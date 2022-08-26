using Discord;
using Discord.WebSocket;

namespace Website.Data;

public class DiscordService
{
	public DiscordService(IConfiguration config, WhazzupService whazzup) => _ = LaunchAsync(config["discord:token"], whazzup);

	private readonly DiscordSocketClient _client = new();

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
			result = component.Data.CustomId switch { "yes" => true, "no" => false, _ => throw new ArgumentException() };
			await component.Message.DeleteAsync();
			mres.Set();
		}

		_client.ButtonExecuted += yesNoButtonClicked;
		await stc.SendMessageAsync(text: query, components: new ComponentBuilder().WithButton("Yes", "yes", ButtonStyle.Success).WithButton("No", "no", ButtonStyle.Danger).Build());

		await Task.Run(() => mres.WaitOne());
		return result;
	}

	private SocketTextChannel? FindChannelByName(string channelName) =>
		_client.Guilds.SelectMany(g => g.Channels.Where(c => c is SocketTextChannel)).FirstOrDefault(c => c.Name == channelName) as SocketTextChannel;

	private async Task LaunchAsync(string token, WhazzupService whazzup)
	{
		HashSet<int> trackedControllers = new();

		_client.Log += LogAsync;
		_client.Ready += async () =>
		{
			if (FindChannelByName("bot-log") is SocketTextChannel channel)
			{
				ulong trackingMessage = (await channel.SendMessageAsync(text: "Online controllers:")).Id;
				static Embed genEmbed(int vid) => new EmbedBuilder().WithCurrentTimestamp().WithDescription($"[{vid} Member Page](https://ivao.aero/member?Id={vid})").WithImageUrl($"https://status.ivao.aero/{vid}.png").Build();
				Task updateAsync() => channel.ModifyMessageAsync(trackingMessage, mp => { mp.Content = "Online controllers:"; if (trackedControllers.Any()) mp.Embeds = new(trackedControllers.Select(genEmbed).ToArray()); else mp.Content += " None"; });

				whazzup.AtcConnected += async controller =>
				{
					trackedControllers.Add(controller.UserId);
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
