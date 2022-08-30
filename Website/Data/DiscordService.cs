using Discord;
using Discord.WebSocket;

using Microsoft.EntityFrameworkCore;

using System.Text.RegularExpressions;

namespace Website.Data;

public class DiscordService
{
	public DiscordService(IConfiguration config, WhazzupService whazzup, IDbContextFactory<WebsiteContext> webContextFactory)
	{
		_webContextFactory = webContextFactory;
		_ = LaunchAsync(config["discord:token"], whazzup);
	}

	internal readonly DiscordSocketClient _client = new(new DiscordSocketConfig() { GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.GuildMembers });
	private readonly IDbContextFactory<WebsiteContext> _webContextFactory;

	public async Task<bool> SendMessageAsync(string channelName, string message)
	{
		if (FindChannelByName(channelName) is not SocketTextChannel stc)
			return false;

		await stc.SendMessageAsync(text: message);
		return true;
	}

	public async Task<bool> SendMessageAsync(ulong channelId, string message)
	{
		if (await _client.GetChannelAsync(channelId) is not SocketTextChannel stc)
			return false;

		await stc.SendMessageAsync(text: message);
		return true;
	}

	/// <summary>Sends a yes/no prompt to the given channel.</summary>
	/// <returns><see langword="true"/> if a user pressed Yes, <see langword="false"/> if they pressed No</returns>
	public async Task<bool> RequestYesNoAsync(string channelName, string query)
	{
		if (FindChannelByName(channelName) is not SocketTextChannel stc)
			return false;

		return await RequestYesNoAsync(stc.Id, query);
	}

	/// <summary>Sends a yes/no prompt to the given channel.</summary>
	/// <returns><see langword="true"/> if a user pressed Yes, <see langword="false"/> if they pressed No</returns>
	public async Task<bool> RequestYesNoAsync(ulong channelId, string query)
	{
		if (await _client.GetChannelAsync(channelId) is not SocketTextChannel stc)
			throw new ArgumentException("Unknown channel id", nameof(channelId));

		AutoResetEvent mres = new(false);
		bool result = false;
		async Task yesNoButtonClicked(SocketMessageComponent component)
		{
			_client.ButtonExecuted -= yesNoButtonClicked;
			result = component.Data.CustomId switch { "yes" => true, "no" => false, _ => throw new ArgumentException("Button ids must be yes & no", nameof(component)) };
			await component.Message.DeleteAsync();
			mres.Set();
		}

		_client.ButtonExecuted += yesNoButtonClicked;
		await stc.SendMessageAsync(text: query, components: new ComponentBuilder().WithButton("Yes", "yes", ButtonStyle.Success).WithButton("No", "no", ButtonStyle.Danger).Build());

		await Task.Run(() => mres.WaitOne());
		return result;
	}

	public async Task EnforceRolesAsync(User user, IvaoApiService api)
	{
		if (user.Snowflake is null)
			return;

		static IEnumerable<ulong> roleToSnowflakes(DiscordRoles roles)
		{
			if (roles.HasFlag(DiscordRoles.Member))
				yield return 1012842226692407366UL; // bot-1
			if (roles.HasFlag(DiscordRoles.Staff))
				yield return 1012842187848945786UL; // bot-2
			if (roles.HasFlag(DiscordRoles.Controller))
				yield return 1012842152797151282UL; // bot-4
			if (roles.HasFlag(DiscordRoles.Pilot))
				yield return 1012842085684105318UL; // bot-8
			if (roles.HasFlag(DiscordRoles.Training))
				yield return 1012842037705445496UL; // bot-64
			if (roles.HasFlag(DiscordRoles.Membership))
				yield return 1012841935339278428UL; // bot-128
			if (roles.HasFlag(DiscordRoles.Administrator))
				yield return 1012841779961282661UL; // bot-max
		}

		var ivao = _client.Guilds.Single();
		_client.PurgeUserCache();
		await _client.DownloadUsersAsync(new[] { ivao });
		if (ivao.GetUser(user.Snowflake.Value) is not IGuildUser igu)
			return;

		Regex trainer = new(@"\bXA-T[A0]");
		Regex membership = new(@"\bXA-M(AC?|C)");
		void setFlag(bool set, DiscordRoles flag)
		{
			if (set)
				user.Roles |= flag;
			else
				user.Roles &= ~flag;
		}

		setFlag(user.Staff is not null && trainer.IsMatch(user.Staff), DiscordRoles.Training);
		setFlag(user.Staff is not null && membership.IsMatch(user.Staff), DiscordRoles.Membership);
		setFlag((await api.GetCountriesAsync()).SelectMany(c => new[] { c.id, c.divisionId }).Contains(user.Division), DiscordRoles.Member);

		await igu.AddRolesAsync(roleToSnowflakes(user.Roles).ToArray());
	}

	private SocketTextChannel? FindChannelByName(string channelName) =>
		_client.Guilds.SelectMany(g => g.Channels.Where(c => c is SocketTextChannel)).FirstOrDefault(c => c.Name == channelName) as SocketTextChannel;

	private async Task QuietlyCatalogueUserAsync(IGuildUser igu)
	{
		if (!igu.DisplayName.Contains('|'))
			return;

		string potVid = igu.Nickname?.Split('|', StringSplitOptions.TrimEntries)[^1] ?? "";
		if (!int.TryParse(potVid, out int vid))
			return;

		var webContext = _webContextFactory.CreateDbContext();
		if (await webContext.Users.FindAsync(vid) is User u && u.Snowflake is null)
			u.Snowflake = igu.Id;
		else
			await webContext.Users.AddAsync(new() { Vid = vid, Snowflake = igu.Id, Roles = 0 });

		await webContext.SaveChangesAsync();
	}

	private async Task LaunchAsync(string token, WhazzupService whazzup)
	{
		Dictionary<int, User?> trackedControllers = new();

		_client.Log += LogAsync;
		_client.MessageReceived += async msg =>
		{
			if (msg.Author is IGuildUser igu)
				await QuietlyCatalogueUserAsync(igu);

			foreach (var user in msg.MentionedUsers)
				if (user is IGuildUser igu2)
					await QuietlyCatalogueUserAsync(igu2);
		};

		_client.Ready += async () =>
		{
			if (FindChannelByName("bot-log") is SocketTextChannel channel)
			{
				ulong trackingMessage = (await channel.SendMessageAsync(text: "Online controllers: None")).Id;

				static Embed genEmbed(KeyValuePair<int, User?> user) => new EmbedBuilder().WithCurrentTimestamp().WithDescription($"[{user.Value?.Mention ?? user.Key.ToString()} Member Page](https://ivao.aero/member?Id={user.Key})").WithImageUrl($"https://status.ivao.aero/{user.Key}.png?time={DateTime.UtcNow.Ticks}").Build();
				Task updateAsync() => channel.ModifyMessageAsync(trackingMessage, mp => { mp.Content = "Online controllers:"; if (trackedControllers.Any()) mp.Embeds = new(trackedControllers.Select(genEmbed).ToArray()); else mp.Content += " None"; });

				whazzup.AtcConnected += async controller =>
				{
					var context = await _webContextFactory.CreateDbContextAsync();
					trackedControllers.Add(controller.UserId, context.Users.AsNoTracking().First(u => u.Vid == controller.UserId));
					await updateAsync();
				};

				whazzup.AtcDisconnected += async controller =>
				{
					trackedControllers.Remove(controller.UserId);
					await updateAsync();
				};

				_ = Task.Run(async () =>
				{
					try
					{
						while (true)
						{
							await updateAsync();
							await Task.Delay(TimeSpan.FromSeconds(30));
						}
					}
					catch { }
					finally { await channel.DeleteMessageAsync(trackingMessage); }
				});
			}
		};

		await _client.LoginAsync(TokenType.Bot, token);
		await _client.StartAsync();

		await Task.Delay(-1);
	}

	const string LOG_FILE = "discord.log";
	private Task LogAsync(LogMessage msg)
	{
		lock (LOG_FILE)
		{
			File.AppendAllLines(LOG_FILE, new[] { msg.ToString() + Environment.NewLine });
		}
		return Task.CompletedTask;
	}
}
