using Discord;
using Discord.WebSocket;

using Microsoft.EntityFrameworkCore;

using System.Text.Json;
using System.Text.RegularExpressions;

namespace Website.Data;

public partial class DiscordService
{
	public const string CHANNEL_CONF_PATH = "discord_channels.json";
	public const string ROLE_CONF_PATH = "discord_roles.txt";

	public DiscordService(IConfiguration config, WhazzupService whazzup, IDbContextFactory<WebsiteContext> webContextFactory)
	{
		_webContextFactory = webContextFactory;
		_ = LaunchAsync(config["discord:token"] ?? throw new KeyNotFoundException(), whazzup);
	}

	internal readonly DiscordSocketClient _client = new(new DiscordSocketConfig() { GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.GuildMembers });
	private readonly IDbContextFactory<WebsiteContext> _webContextFactory;

	private readonly Dictionary<DiscordRoles, string> _roles = new()
	{
		{ DiscordRoles.Administrator, "administrator" },
		{ DiscordRoles.Membership, "membership" },
		{ DiscordRoles.Training, "training" },
		{ DiscordRoles.Events, "events" },
		{ DiscordRoles.Staff, "staff" },
		{ DiscordRoles.Pilot, "pilot" },
		{ DiscordRoles.Controller, "controller" },
		{ DiscordRoles.Member, "member" },
	};

	public async Task<bool> SendMessageAsync(User user, string message, int? deleteAfter = null)
	{
		if (deleteAfter is int msdelay && msdelay < 0)
			throw new ArgumentOutOfRangeException(nameof(deleteAfter));

		if (user.Snowflake is null || _client.Guilds.Single().GetUser(user.Snowflake.Value) is not SocketGuildUser sgu)
			return false;

		var msg = await sgu.SendMessageAsync(message);

		if (deleteAfter is int da)
		{
			await Task.Delay(da);
			await msg.DeleteAsync();
		}
		return true;
	}

	public async Task<bool> SendMessageAsync(IUser user, string message, int? deleteAfter = null)
	{
		if (deleteAfter is int msdelay && msdelay < 0)
			throw new ArgumentOutOfRangeException(nameof(deleteAfter));

		var msg = await user.SendMessageAsync(text: message);

		if (deleteAfter is int da)
		{
			await Task.Delay(da);
			await msg.DeleteAsync();
		}
		return true;
	}

	public async Task<bool> SendMessageAsync(string channelName, string message, int? deleteAfter = null)
	{
		if (FindTextChannelByName(channelName) is not SocketTextChannel stc)
			return false;

		var msg = await stc.SendMessageAsync(text: message);

		if (deleteAfter is int da)
		{
			await Task.Delay(da);
			await msg.DeleteAsync();
		}
		return true;
	}

	public async Task<bool> SendMessageAsync(ulong channelId, string message, int? deleteAfter = null)
	{
		if (await _client.GetChannelAsync(channelId) is not SocketTextChannel stc)
			return false;

		var msg = await stc.SendMessageAsync(text: message);

		if (deleteAfter is int da)
		{
			await Task.Delay(da);
			await msg.DeleteAsync();
		}
		return true;
	}

	/// <summary>Sends a yes/no prompt to the given botlog.</summary>
	/// <returns><see langword="true"/> if a user pressed Yes, <see langword="false"/> if they pressed No</returns>
	public async Task<bool> RequestYesNoAsync(string channelName, string query)
	{
		if (FindTextChannelByName(channelName) is not SocketTextChannel stc)
			return false;

		return await RequestYesNoAsync(stc.Id, query);
	}

	/// <summary>Sends a yes/no prompt to the given botlog.</summary>
	/// <returns><see langword="true"/> if a user pressed Yes, <see langword="false"/> if they pressed No</returns>
	public async Task<bool> RequestYesNoAsync(ulong channelId, string query)
	{
		if (await _client.GetChannelAsync(channelId) is not SocketTextChannel stc)
			throw new ArgumentException("Unknown botlog id", nameof(channelId));

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

		await Task.Run(mres.WaitOne);
		return result;
	}

	public async Task RequestSnowflakeAsync()
	{
		if (FindTextChannelByName("verify") is not SocketTextChannel verifyChannel)
			return;

		async Task buttonClicked(SocketMessageComponent component)
		{
			if (component.Data.Type != ComponentType.Button || component.Data.CustomId != "connect")
				return;

			await component.DeferAsync();

			string reglink =
#if DEBUG
				"http://52.43.225.251/register/" + component.User.Id;
#else
				"http://xa.ivao.aero/register/" + component.User.Id;
#endif

			try
			{
				var msg = await SendMessageAsync(component.User, $"{component.User.Mention}, go to {reglink} to sign in through the website and get your discord roles!", 60000);
			}
			catch
			{
				var msg = await SendMessageAsync(component.Channel.Id, $"{component.User.Mention}, go to {reglink} to sign in through the website and get your discord roles!", 5000);
			}
		}

		_client.ButtonExecuted += buttonClicked;

		await foreach (var msgs in verifyChannel.GetMessagesAsync())
			await verifyChannel.DeleteMessagesAsync(msgs);

		await verifyChannel.SendMessageAsync(text: "Click the button to receive a message with instructions on how to connect to the website and receive your roles.", components: new ComponentBuilder().WithButton("Connect", "connect").Build());
	}

	public async Task<DiscordRoles> EnforceRolesAsync(User user, IvaoApiService api)
	{
		var ivao = _client.Guilds.Single();

		void setFlag(bool set, DiscordRoles flag)
		{
			if (set)
				user.Roles |= flag;
			else
				user.Roles &= ~flag;
		}

		setFlag(user.Staff is not null && TrainerRegex().IsMatch(user.Staff), DiscordRoles.Training);
		setFlag(user.Staff is not null && MembershipRegex().IsMatch(user.Staff), DiscordRoles.Membership);
		setFlag(user.Staff is not null && EventsRegex().IsMatch(user.Staff), DiscordRoles.Events);
		setFlag(user.Staff is not null && AdministratorRegex().IsMatch(user.Staff), DiscordRoles.Administrator);

		bool isMember = (await api.GetCountriesAsync()).SelectMany(c => new[] { c.id, c.divisionId }).Contains(user.Division);
		setFlag(isMember && !string.IsNullOrEmpty(user.Staff), DiscordRoles.Staff);
		setFlag(isMember, DiscordRoles.Member);

		if (user.Snowflake is null)
			return user.Roles;

		IEnumerable<SocketRole> roleToSnowflakes(DiscordRoles roles, AtcRating? atcRating = null, PilotRating? pilotRating = null)
		{
			yield return ivao.Roles.Single(r => r.Name.Equals("linked", StringComparison.InvariantCultureIgnoreCase));

			for (int shift = 0; shift < 64; ++shift)
				if (roles.HasFlag((DiscordRoles)((ulong)1 << shift)))
					yield return ivao.Roles.Single(r => r.Name.Equals(_roles[(DiscordRoles)((ulong)1 << shift)], StringComparison.InvariantCultureIgnoreCase));

			if (!roles.HasFlag(DiscordRoles.Member))
				yield return ivao.Roles.Single(r => r.Name.Equals("visitor", StringComparison.InvariantCultureIgnoreCase));

			if (atcRating is AtcRating ar)
				yield return ivao.Roles.Single(r => r.Name.Equals(Enum.GetName((AtcRating)Math.Min((int)ar, (int)AtcRating.SEC)) switch { "SEC" => "SEC+", string a => a, _ => throw new Exception() }, StringComparison.InvariantCultureIgnoreCase));

			if (pilotRating is PilotRating pr)
				yield return ivao.Roles.Single(r => r.Name.Equals(Enum.GetName((PilotRating)Math.Min((int)pr, (int)PilotRating.ATP)) switch { "ATP" => "ATP+", string a => a, _ => throw new Exception() }, StringComparison.InvariantCultureIgnoreCase));
		}

		_client.PurgeUserCache();
		await _client.DownloadUsersAsync(new[] { ivao });
		ivao = _client.Guilds.Single();

		var guildUser = ivao.GetUser(user.Snowflake.Value);
		if (guildUser is not IGuildUser igu)
			return user.Roles;

#if DEBUG
		await igu.AddRolesAsync(roleToSnowflakes(user.Roles, user.RatingAtc, user.RatingPilot));
#else
		static string staffPos(string[] positions)
		{
			var hqPositions = positions.Where(p => !p.Contains('-')).ToArray();
			var divMainPositions = positions.Where(p => p.Contains('-') && !p.Any(char.IsDigit));
			var assistantPositions = positions.Where(p => p.Contains('-') && p.Any(char.IsDigit));
			var divPositions = divMainPositions.Concat(assistantPositions).ToArray();

			string sp =
				string.Join(' ',
					hqPositions.Any()
					? divPositions.Any()
					  ? hqPositions
						.Concat(
							divPositions[1..]
							.Select(p => p.IndexOf('-') == 2 ? p[3..] : p)
							.Prepend(divPositions[0])
						)
					  : hqPositions
					: divPositions
				);

			return sp.TrimStart();
		}

		await igu.ModifyAsync(gup =>
		{
			gup.Roles = new(roleToSnowflakes(user.Roles, user.RatingAtc, user.RatingPilot));

			if (string.IsNullOrWhiteSpace(user.Staff))
				gup.Nickname = new($"{user.FirstName} | {user.Vid}");
			else
				gup.Nickname = new($"{user.FirstName} | {staffPos(user.Staff.Split(':'))}");
		});
#endif

		return user.Roles;
	}

	public async Task EnforceAllRolesAsync(WebsiteContext db, IvaoApiService api)
	{
		var ivao = _client.Guilds.Single();
		_client.PurgeUserCache();
		await _client.DownloadUsersAsync(new[] { ivao });
		ivao = _client.Guilds.Single();

		static void setFlag(User user, bool set, DiscordRoles flag)
		{
			if (set)
				user.Roles |= flag;
			else
				user.Roles &= ~flag;
		}

		foreach (var user in db.Users.Where(u => u.Snowflake != null)
#if DEBUG
			.Where(u => u.Roles.HasFlag(DiscordRoles.Administrator))
#endif
			)
		{
			setFlag(user, user.Staff is not null && TrainerRegex().IsMatch(user.Staff), DiscordRoles.Training);
			setFlag(user, user.Staff is not null && MembershipRegex().IsMatch(user.Staff), DiscordRoles.Membership);
			setFlag(user, user.Staff is not null && EventsRegex().IsMatch(user.Staff), DiscordRoles.Events);
			setFlag(user, user.Staff is not null && AdministratorRegex().IsMatch(user.Staff), DiscordRoles.Administrator);

			bool isMember = (await api.GetCountriesAsync()).SelectMany(c => new[] { c.id, c.divisionId }).Contains(user.Division);
			setFlag(user, isMember && !string.IsNullOrEmpty(user.Staff), DiscordRoles.Staff);
			setFlag(user, isMember, DiscordRoles.Member);

			if (user.Snowflake is null)
				continue;

			IEnumerable<SocketRole> roleToSnowflakes(DiscordRoles roles, AtcRating? atcRating = null, PilotRating? pilotRating = null)
			{
				yield return ivao.Roles.Single(r => r.Name.Equals("linked", StringComparison.InvariantCultureIgnoreCase));

				for (int shift = 0; shift < 64; ++shift)
					if (roles.HasFlag((DiscordRoles)((ulong)1 << shift)))
						yield return ivao.Roles.Single(r => r.Name.Equals(_roles[(DiscordRoles)((ulong)1 << shift)], StringComparison.InvariantCulture));

				if (!roles.HasFlag(DiscordRoles.Member))
					yield return ivao.Roles.Single(r => r.Name.Equals("visitor", StringComparison.InvariantCultureIgnoreCase));

				if (atcRating is AtcRating ar)
					yield return ivao.Roles.Single(r => r.Name.Equals(Enum.GetName((AtcRating)Math.Min((int)ar, (int)AtcRating.SEC)) switch { "SEC" => "SEC+", string a => a, _ => throw new Exception() }, StringComparison.InvariantCultureIgnoreCase));

				if (pilotRating is PilotRating pr)
					yield return ivao.Roles.Single(r => r.Name.Equals(Enum.GetName((PilotRating)Math.Min((int)pr, (int)PilotRating.ATP)) switch { "ATP" => "ATP+", string a => a, _ => throw new Exception() }, StringComparison.InvariantCultureIgnoreCase));
			}

			var guildUser = ivao.GetUser(user.Snowflake.Value);
			if (guildUser is not IGuildUser igu)
				continue;

#if !DEBUG
		await igu.RemoveRolesAsync(igu.RoleIds);
#endif

			await igu.AddRolesAsync(roleToSnowflakes(user.Roles, user.RatingAtc, user.RatingPilot).ToArray());
		}

		await db.SaveChangesAsync();
	}

	private SocketTextChannel? FindTextChannelByName(string channelName) =>
		_client.Guilds.SelectMany(g => g.Channels.Where(c => c is SocketTextChannel))
#if DEBUG
		.FirstOrDefault(c => c.Name.Equals("test-" + channelName, StringComparison.InvariantCultureIgnoreCase))
#else
		.FirstOrDefault(c => c.Name == channelName)
#endif
		as SocketTextChannel;
	private SocketVoiceChannel? FindVoiceChannelByName(string channelName) =>
		_client.Guilds.SelectMany(g => g.Channels.Where(c => c is SocketVoiceChannel))
#if DEBUG
		.FirstOrDefault(c => c.Name == "[TEST] " + channelName)
#else
		.FirstOrDefault(c => c.Name == channelName)
#endif
		as SocketVoiceChannel;
	private SocketCategoryChannel? FindCategoryByName(string categoryName) =>
		_client.Guilds.SelectMany(g => g.CategoryChannels)
#if DEBUG
		.FirstOrDefault(c => c.Name == (categoryName.StartsWith('[') ? categoryName : $"[-BOT-{categoryName}-]"));
#else
		.FirstOrDefault(c => c.Name == (categoryName.StartsWith('[') ? categoryName : $"[-{categoryName}-]"));
#endif

	private async Task QuietlyCatalogueUserAsync(IGuildUser igu)
	{
		if (!(igu.DisplayName?.Contains('|') ?? false))
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
		Dictionary<ATC, User?> trackedControllers = new();
		Dictionary<ATC, ulong> trainingExamAnnouncements = new();

		_client.Log += LogAsync;
		_client.MessageReceived += async msg =>
		{
			if (msg.Author is IGuildUser igu)
				await QuietlyCatalogueUserAsync(igu);

			foreach (var user in msg.MentionedUsers)
				if (user is IGuildUser igu2)
					await QuietlyCatalogueUserAsync(igu2);
		};

		_client.SlashCommandExecuted += async command =>
		{
			await command.DeferAsync(true);
			SocketSlashCommandDataOption getOption(string option) =>
				command.Data.Options.Where(c => c.Name == option).Single();

#if DEBUG
			if (!command.Data.Name.StartsWith("debug-"))
				return;

			switch (command.Data.Name["debug-".Length..])
#else
			switch (command.Data.Name)
#endif
			{
				case "train-atc":
				case "train-pilot":
					bool pilot = command.Data.Name == "train-pilot";

					var trainContext = await _webContextFactory.CreateDbContextAsync();
					var trainerUser = (SocketGuildUser)command.User;
					var traineeUser = (SocketGuildUser)getOption("trainee").Value;

					var trainer = await trainContext.Users.SingleOrDefaultAsync(u => u.Snowflake == trainerUser.Id);
					var trainee = await trainContext.Users.SingleOrDefaultAsync(u => u.Snowflake == traineeUser.Id);

					if (trainer is null)
					{
						await command.ModifyOriginalResponseAsync(r => r.Content = $"Couldn't create training. I don't know who you are!");
						return;
					}

					if (!trainer.Roles.HasFlag(DiscordRoles.Training))
					{
						await command.ModifyOriginalResponseAsync(r => r.Content = $"Ask your trainer to make the training for you.");
						return;
					}

					if (trainee is null)
					{
						await command.ModifyOriginalResponseAsync(r => r.Content = $"Looks like the {traineeUser.Mention} isn't in the system. Ask them to run `/register` to join and then try again.");
						return;
					}

					if (pilot)
					{
						var trainingRating = (trainee.RatingPilot ?? PilotRating.FS1) + 1;

						trainContext.TrainingRequests.Add(new() {
							Trainer = trainer.Vid,
							Trainee = trainee.Vid,
							PilotRating = trainingRating,
							Comments = "0/" + Directory.GetFiles(Path.Join("training", "data", Enum.GetName(trainingRating))).Length
						});

						await command.ModifyOriginalResponseAsync(r => r.Content = $"Created an {trainingRating} training for {traineeUser.Mention} (VID: {trainee.Vid})");
					}
					else
					{
						var trainingRating = (trainee.RatingAtc ?? AtcRating.AS1) + 1;

						trainContext.TrainingRequests.Add(new() {
							Trainer = trainer.Vid,
							Trainee = trainee.Vid,
							AtcRating = trainingRating,
							Comments = "0/" + Directory.GetFiles(Path.Join("training", "data", Enum.GetName(trainingRating))).Length
						});

						await command.ModifyOriginalResponseAsync(r => r.Content = $"Created an {trainingRating} training for {traineeUser.Mention} (VID: {trainee.Vid})");
					}

					await trainContext.SaveChangesAsync();
					await UpdateTrainingChannelsAsync(trainContext);
					break;

				case "exam":
					var context = await _webContextFactory.CreateDbContextAsync();
					var examTrainer = context.Users.FirstOrDefault(u => u.Snowflake == ((SocketGuildUser)command.User).Id);

					if (examTrainer is null)
					{
						await command.ModifyOriginalResponseAsync(r => r.Content = "Who are you and how did you get in my channel?");
						break;
					}
					else if (!examTrainer.Roles.HasFlag(DiscordRoles.Training))
					{
						await command.ModifyOriginalResponseAsync(r => r.Content = "Ask the designated trainer to do this for you.");
						break;
					}

					string traineeVidStr = new(command.Channel.Name.TakeWhile(char.IsDigit).ToArray());
					if (traineeVidStr.Length != 6 || !int.TryParse(traineeVidStr, out int traineeVid))
					{
						await command.ModifyOriginalResponseAsync(r => r.Content = "This isn't a training channel!");
						break;
					}

					var assignedTrainings = context.TrainingRequests.Where(tr => tr.Trainer == examTrainer.Vid).AsNoTracking().ToArray();
					if (assignedTrainings.FirstOrDefault(tr => tr.Trainee == traineeVid) is not TrainingRequest req)
					{
						await command.ModifyOriginalResponseAsync(r => r.Content = "You are not the assigned trainer for this channel.");
						break;
					}
					else if (req.AtcRating is null)
					{
						await command.ModifyOriginalResponseAsync(r => r.Content = "Only ATC exams can be scheduled through the bot. Please use this channel for coordinating pilot exams.");
						break;
					}

					if (!DateTime.TryParse((string)getOption("start").Value, out DateTime startTime))
					{
						await command.ModifyOriginalResponseAsync(r => r.Content = $"I couldn't figure out when you wanted the exam to start, sorry! Try using this format: {DateTime.UtcNow}");
						break;
					}

					await context.Exams.AddAsync(new() {
						Rating = req.AtcRating.Value,
						Trainer = req.Trainer!.Value,
						Trainee = req.Trainee,
						Mock = false,
						Start = startTime,
						Position = (string)getOption("position").Value
					});

					await command.ModifyOriginalResponseAsync(r => r.Content = "Done! It's been scheduled.");
					break;

				default:
					throw new NotImplementedException("Unrecognised Command");
			}
		};

		_client.Ready += async () =>
		{
			await EnforceDiscordConfigAsync();
			var ivao = _client.Guilds.Single();
			await ivao.DeleteApplicationCommandsAsync();

			// Running online controllers view
			if (FindCategoryByName("ONLINE") is SocketCategoryChannel onlineCategory)
			{
				SocketTextChannel? botlog = FindTextChannelByName("active-controllers");

				ulong? trackingMessage = (await (botlog?.SendMessageAsync("Online controllers: None") ?? Task.FromResult<Discord.Rest.RestUserMessage?>(null)))?.Id;

				static Embed genEmbed(KeyValuePair<ATC, User?> user, string uniqueData) =>
					new EmbedBuilder()
					.WithCurrentTimestamp()
					.WithDescription($"[{user.Value?.Mention ?? user.Key.ToString()} Member Page](https://ivao.aero/Member.aspx?Id={user.Key.UserId})")
					.WithImageUrl($"https://status.ivao.aero/{user.Key.UserId}.png?time={uniqueData}").Build();

				async Task updateAsync()
				{
					botlog = FindTextChannelByName("active-controllers");

					foreach (var c in onlineCategory.Channels.Where(c => c.Id != botlog?.Id && !trackedControllers.Any(v => v.Key.Callsign == c.Name)))
						await c.DeleteAsync();

					await (
						botlog?.ModifyMessageAsync(
							trackingMessage!.Value,
							mp =>
							{
								mp.Content = "Online controllers:";
								if (trackedControllers.Any())
									mp.Embeds = new(trackedControllers.Select(v => genEmbed(v, DateTime.UtcNow.Ticks.ToString()[^5..])).ToArray());
								else
									mp.Content += " None";
							}
						) ?? Task.CompletedTask
					);

					var context = await _webContextFactory.CreateDbContextAsync();
					await UpdateTrainingChannelsAsync(context);
					await UpdateEventChannelsAsync(context);
				}

#pragma warning disable SYSLIB1045 // Convert to 'GeneratedRegexAttribute'.
				Regex trainerExaminerCallsign = new(@"^[A-Z]{4}_[XT]_(TWR|APP|CTR)$", RegexOptions.ExplicitCapture | RegexOptions.Compiled);
#pragma warning restore SYSLIB1045 // Convert to 'GeneratedRegexAttribute'.

				async Task whazzup_ConnectedAsync(ATC controller)
				{
					var context = await _webContextFactory.CreateDbContextAsync();

					if (trainerExaminerCallsign.IsMatch(controller.Callsign) && FindTextChannelByName("trainings-exams") is SocketTextChannel announcementChannel)
						trainingExamAnnouncements.Add(controller, (await announcementChannel.SendMessageAsync(text: $"<@{_roles[DiscordRoles.Announcement]}>! Come join us for a {(controller.Callsign.Split('_')[1] == "X" ? "training" : "exam")} at {controller.Callsign.Split('_')[0]}.")).Id);
					else
					{
						trackedControllers.Add(controller, context.Users.AsNoTracking().First(u => u.Vid == controller.UserId));
						List<string> admins = new() { "administrator", "Bots" };
						if (trackedControllers[controller] is User u && u.Snowflake is ulong s)
							admins.Add(s.ToString());

						await ivao.CreateVoiceChannelAsync(controller.Callsign, vcp => { vcp.CategoryId = onlineCategory.Id; vcp.PermissionOverwrites = new(GetOverwrites(ivao, new() { Deny = new[] { "*" }, Read = Array.Empty<string>(), Write = new[] { "member" }, Admin = admins.ToArray() })); });
					}
					await updateAsync();
				}

				async Task whazzup_DisconnectedAsync(ATC controller)
				{
					foreach (var c in trackedControllers.Where(tc => tc.Value is not null && tc.Value.Vid == controller.UserId))
						trackedControllers.Remove(c.Key);

					await (FindVoiceChannelByName(controller.Callsign)?.DeleteAsync() ?? Task.CompletedTask);

					await updateAsync();
				}

				foreach (var atc in whazzup.ConnectedXAControllers)
					await whazzup_ConnectedAsync(atc);

				whazzup.AtcConnected += whazzup_ConnectedAsync;
				whazzup.AtcDisconnected += whazzup_DisconnectedAsync;

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
					finally { foreach (var c in onlineCategory.Channels) await c.DeleteAsync(); await onlineCategory.DeleteAsync(); }
				});
			}

			await ivao.CreateApplicationCommandAsync(new SlashCommandBuilder()
#if DEBUG
				.WithName("debug-train-atc")
#else
				.WithName("train-atc")
#endif
				.WithDescription("Add a training with yourself as the trainer.")
				.AddOption("trainee", ApplicationCommandOptionType.User, "The user requesting the training", true, isAutocomplete: true)
				.Build());

			await ivao.CreateApplicationCommandAsync(new SlashCommandBuilder()
#if DEBUG
				.WithName("debug-train-pilot")
#else
				.WithName("train-pilot")
#endif
				.WithDescription("Add a training with yourself as the trainer.")
				.AddOption("trainee", ApplicationCommandOptionType.User, "The user requesting the training", true, isAutocomplete: true)
				.Build());

			await ivao.CreateApplicationCommandAsync(new SlashCommandBuilder()
#if DEBUG
				.WithName("debug-exam")
#else
				.WithName("exam")
#endif
				.WithDescription("Schedule an exam for this trainee.")
				.AddOption("start", ApplicationCommandOptionType.String, "The start time of the exam", true)
				.AddOption("position", ApplicationCommandOptionType.String, "The position on which the exam will take place", true)
				.Build());

			await RequestSnowflakeAsync();
		};

		await _client.LoginAsync(TokenType.Bot, token);
		await _client.StartAsync();

		await Task.Delay(-1);
	}

	private static IEnumerable<Overwrite> GetOverwrites(SocketGuild ivao, DiscordConfigPermissions perms)
	{
		OverwritePermissions readPerms = new(viewChannel: PermValue.Allow, connect: PermValue.Allow, speak: PermValue.Deny);
		OverwritePermissions writePerms = new(
			viewChannel: PermValue.Allow, sendMessages: PermValue.Allow, sendMessagesInThreads: PermValue.Allow,
			attachFiles: PermValue.Allow, addReactions: PermValue.Allow,
			connect: PermValue.Allow, speak: PermValue.Allow
		);
		OverwritePermissions adminAdditions = new(
			manageMessages: PermValue.Allow, moveMembers: PermValue.Allow,
			muteMembers: PermValue.Allow, deafenMembers: PermValue.Allow,
			prioritySpeaker: PermValue.Allow
		);

		OverwritePermissions adminPerms = new(1 << 3 | adminAdditions.AllowValue | writePerms.AllowValue, 0);

		Dictionary<string, (ulong Id, PermissionTarget Target)> targets = new();
		foreach (var p in perms.Read.Concat(perms.Write).Concat(perms.Admin))
			if (p == "*")
				targets.TryAdd(p, (ivao.EveryoneRole.Id, PermissionTarget.Role));
			else if (ulong.TryParse(p, out ulong rid) && ivao.GetRole(rid) is SocketRole sr1)
				targets.TryAdd(p, (rid, PermissionTarget.Role));
			else if (ivao.Roles.FirstOrDefault(r => r.Name.Equals(p, StringComparison.InvariantCultureIgnoreCase)) is SocketRole sr2)
				targets.TryAdd(p, (sr2.Id, PermissionTarget.Role));
			else if (ulong.TryParse(p, out ulong uid) && ivao.GetUser(uid) is SocketGuildUser su1)
				targets.TryAdd(p, (su1.Id, PermissionTarget.User));
			else if (ivao.Users.FirstOrDefault(u => u.DisplayName.Equals(p, StringComparison.InvariantCultureIgnoreCase)) is SocketGuildUser su2)
				targets.TryAdd(p, (su2.Id, PermissionTarget.User));
			else
				Console.Error.WriteLine("Unknown role/user " + p);

		yield return new Overwrite(ivao.EveryoneRole.Id, PermissionTarget.Role, new(viewChannel: PermValue.Deny));

		foreach (var rp in perms.Read.Where(r => targets.ContainsKey(r)))
			yield return new Overwrite(targets[rp].Id, targets[rp].Target, readPerms);

		foreach (var rp in perms.Write.Where(r => targets.ContainsKey(r)))
			yield return new Overwrite(targets[rp].Id, targets[rp].Target, writePerms);

		foreach (var rp in perms.Admin.Where(r => targets.ContainsKey(r)))
			yield return new Overwrite(targets[rp].Id, targets[rp].Target, adminPerms);
	}

	public async Task EnforceDiscordConfigAsync()
	{
		// Enforce botlog config
		SocketGuild ivao = _client.Guilds.Single();

		if (!File.Exists(CHANNEL_CONF_PATH))
			await File.WriteAllTextAsync(CHANNEL_CONF_PATH, "[]");

		if (!File.Exists(ROLE_CONF_PATH))
			await File.WriteAllTextAsync(ROLE_CONF_PATH, "administrator:FF0000");

		List<(string Name, uint? Color)> roles = new();
		static bool tryParseHex(string hex, out uint value)
		{
			value = 0;
			hex = hex.ToUpperInvariant();

			if (hex.Any(c => !"0123456789ABCDEF".Contains(c)))
				return false;

			value = Convert.ToUInt32(hex, 16);
			return true;
		}

		foreach (string line in File.ReadAllLines(ROLE_CONF_PATH).Select(l => l.Trim()))
			roles.Add(line.Contains(':') && tryParseHex(line[(line.LastIndexOf(':') + 1)..], out uint c) ? (line[..line.LastIndexOf(':')], c) : (line, null));

		// Delete any unwanted roles
#if !DEBUG
		foreach (SocketRole r in ivao.Roles.Where(r => !roles.Any(nr => nr.Name.TrimEnd("*^".ToCharArray()) == r.Name)))
			await r.DeleteAsync();
#endif
		foreach ((string Name, uint? Color) in roles)
			if (!ivao.Roles.Any(r => r.Name == Name.TrimEnd("*^".ToCharArray())))
				// Add missing role
				await ivao.CreateRoleAsync(
					Name.TrimEnd("*^".ToCharArray()),
					Name.EndsWith('*') ? new GuildPermissions(administrator: true) : GuildPermissions.None,
					Color is null ? null : new Color(Color.Value),
					Name.EndsWith('*') || Name.EndsWith('^'),
					true
				);
			else
				// Update existing role
				await ivao.Roles.Single(r => r.Name == Name.TrimEnd("*^".ToCharArray())).ModifyAsync(rp =>
				{
					rp.Permissions = new(Name.EndsWith('*') ? new GuildPermissions(administrator: true) : GuildPermissions.None);
					rp.Color = Color is null ? new() : new(new Color(Color.Value));
					rp.Hoist = Name.EndsWith('*') || Name.EndsWith('^');
					rp.Mentionable = true;
				});

		DiscordConfigCategory[] config = JsonSerializer.Deserialize<DiscordConfigCategory[]>(File.ReadAllText(CHANNEL_CONF_PATH)) ?? throw new Exception("Invalid Discord config.");

		// Delete any unwanted categories.
#if !DEBUG
			while (ivao.CategoryChannels.Any(c => !config.Any(cc => c.Name == cc.Name)))
				await ivao.GetCategoryChannel(ivao.CategoryChannels.First(c => !config.Any(cc => c.Name == cc.Name)).Id)?.DeleteAsync();
#endif

		// Check all current categories
		foreach (var category in config.Select(cat =>
#if DEBUG
				cat with { Name = $"[-BOT-{cat.Name}-]" }
#else
				cat with { Name = $"[-{cat.Name}-]" }
#endif
			))
		{
			// Get/create the category
			if (FindCategoryByName(category.Name) is not SocketCategoryChannel scc)
			{
				await ivao.CreateCategoryChannelAsync(category.Name, gcp => gcp.PermissionOverwrites = new(new Overwrite[] { new(ivao.EveryoneRole.Id, PermissionTarget.Role, new(viewChannel: PermValue.Deny)) }));
				if (FindCategoryByName(category.Name) is not SocketCategoryChannel tmp)
					continue;

				scc = tmp;
			}

			// Enforce category permissions
			await scc.ModifyAsync(gcp => gcp.PermissionOverwrites = new(GetOverwrites(ivao, category.Permissions)));

			// Delete any unwanted channels
			string[] catNames = category.Channels.Select(cc => cc.Name).ToArray();
			foreach (var channel in scc.Channels.Where(c => !catNames.Contains(c.Name) || c is SocketTextChannel && category.Channels.First(cc => cc.Name == c.Name).Voice || c is SocketVoiceChannel && !category.Channels.First(cc => cc.Name == c.Name).Voice))
				await channel.DeleteAsync();

			foreach (var channel in category.Channels
#if DEBUG
				.Select(c => c with { Name = "[TEST] " + c.Name })
#endif
				)
				if (channel.Voice)
				{
					if (FindVoiceChannelByName(channel.Name) is not SocketVoiceChannel svc)
					{
						var rvc = await ivao.CreateVoiceChannelAsync(channel.Name, vcp => vcp.CategoryId = new(scc.Id));
						do
							svc = ivao.GetVoiceChannel(rvc.Id);
						while (svc is null);
					}

					await svc.SyncPermissionsAsync();
					await svc.ModifyAsync(acp => { acp.PermissionOverwrites = new(GetOverwrites(ivao, channel.Permissions)); acp.CategoryId = new(scc.Id); if (channel.Limit > 0) acp.UserLimit = new(channel.Limit); });
				}
				else
				{
					if (FindTextChannelByName(channel.Name) is not SocketTextChannel stc)
					{
						var rtc = await ivao.CreateTextChannelAsync(channel.Name, tcp => tcp.CategoryId = new(scc.Id));
						do
							stc = ivao.GetTextChannel(rtc.Id);
						while (stc is null);
					}

					await stc.ModifyAsync(tcp => { tcp.PermissionOverwrites = new(GetOverwrites(ivao, channel.Permissions)); tcp.CategoryId = new(scc.Id); });

					if (channel.Messages is not null)
					{
						await foreach (var msgs in stc.GetMessagesAsync())
							await stc.DeleteMessagesAsync(msgs);

						foreach (string msg in channel.Messages)
							await stc.SendMessageAsync(msg);
					}
				}
		}
	}

	public async Task UpdateTrainingChannelsAsync(WebsiteContext context)
	{
		var category = FindCategoryByName("TRAINING");
		var ivao = _client.Guilds.Single();

		if (category is null)
		{
			await ivao.CreateCategoryChannelAsync(
#if DEBUG
				"[-BOT-TRAINING-]"
#else
				"[-TRAINING-]"
#endif
			);
			category = FindCategoryByName("TRAINING")!;
		}

		var knownChannels = category.Channels.Select(c => c.Name).ToHashSet();

		foreach (var tr in context.TrainingRequests.AsNoTracking().ToArray())
		{
			string channelName = $"{tr.Trainee} ({tr.AtcRating?.ToString() ?? tr.PilotRating!.ToString()})";
			if (knownChannels.Contains(channelName))
			{
				knownChannels.Remove(channelName);
				continue;
			}

			var trainee = await context.Users.FindAsync(tr.Trainee);
			var trainer = await context.Users.FindAsync(tr.Trainer);

			if (trainee is null || trainer is null || trainee.Snowflake is null || trainer.Snowflake is null)
				continue;

			await ivao.CreateVoiceChannelAsync(channelName, tcp => { tcp.CategoryId = new(category.Id); tcp.PermissionOverwrites = new(GetOverwrites(ivao, new() { Write = new[] { trainee.Snowflake.Value.ToString(), trainer.Snowflake.Value.ToString() } })); });
		}

		var anHour = TimeSpan.FromHours(1);
		foreach (var ex in context.Exams.AsNoTracking().ToArray().Where(ex => ex.Start - anHour < DateTime.UtcNow && DateTime.UtcNow < ex.End + anHour))
		{
			var trainee = await context.Users.FindAsync(ex.Trainee);
			var trainer = await context.Users.FindAsync(ex.Trainer);

			if (trainee is null || trainer is null || trainee.Snowflake is null || trainer.Snowflake is null)
				continue;

			string channelName = $"{trainee.FirstName}'s {ex.Name}";
			if (knownChannels.Contains(channelName))
			{
				knownChannels.Remove(channelName);
				continue;
			}

			List<string> viewingRoles = new() { "SEC+" };
			for (AtcRating rat = AtcRating.ACC; rat >= ex.Rating; --rat)
				viewingRoles.Add(rat.ToString());

			await ivao.CreateVoiceChannelAsync(channelName, tcp => { tcp.CategoryId = new(category.Id); tcp.PermissionOverwrites = new(GetOverwrites(ivao, new() { Read = viewingRoles.ToArray(), Write = new[] { trainee.Snowflake.Value.ToString(), trainer.Snowflake.Value.ToString(), _roles[DiscordRoles.Training] } })); });
		}

		foreach (var deletedChannel in knownChannels)
			await (FindVoiceChannelByName(deletedChannel)?.DeleteAsync() ?? Task.CompletedTask);
	}

	public async Task UpdateEventChannelsAsync(WebsiteContext context)
	{
		var category = FindCategoryByName("EVENTS");
		var ivao = _client.Guilds.Single();

		while (category is null)
		{
			await ivao.CreateCategoryChannelAsync(
#if DEBUG
				"[-BOT-EVENTS-]"
#else
				"[-EVENTS-]"
#endif
			);
			category = FindCategoryByName("EVENTS")!;
		}

		var knownChannels = category.Channels.Select(c => c.Name).ToHashSet();
		var halfHour = TimeSpan.FromMinutes(30);

		foreach (var @event in context.Events.AsNoTracking().ToArray().Where(e => e.Start - halfHour < DateTime.UtcNow && DateTime.UtcNow < e.End + halfHour))
		{
			string channelName = "event-" + @event.Name;
			if (knownChannels.Contains(channelName))
			{
				knownChannels.Remove(channelName);
				continue;
			}

			string[] controllerSnowflakes = @event.Controllers.Split(':').Where(c => c.All(char.IsDigit)).Select(vid => context.Users.FirstOrDefault(u => u.Vid == int.Parse(vid))?.Snowflake?.ToString()).Where(v => v is not null).Cast<string>().ToArray();
			await ivao.CreateVoiceChannelAsync(channelName, tcp => { tcp.CategoryId = new(category.Id); tcp.PermissionOverwrites = new(GetOverwrites(ivao, new() { Write = controllerSnowflakes })); });
		}

		foreach (var deletedChannel in knownChannels)
			await (FindVoiceChannelByName(deletedChannel)?.DeleteAsync() ?? Task.CompletedTask);
	}

	const string LOG_FILE = "discord.log";
	private Task LogAsync(LogMessage msg)
	{
		lock (LOG_FILE)
			File.AppendAllLines(LOG_FILE, new[] { msg.ToString() + Environment.NewLine });
		return Task.CompletedTask;
	}

	[GeneratedRegex("\\bXA-(T[CA0]|A?DIR)")]
	private static partial Regex TrainerRegex();
	[GeneratedRegex("\\bXA-(M(AC?|C)|A?DIR)")]
	private static partial Regex MembershipRegex();
	[GeneratedRegex("\\bXA-(E(AC?|C)|A?DIR)")]
	private static partial Regex EventsRegex();
	[GeneratedRegex("\\bXA-(A?WM|WMA\\d|A?DIR)")]
	private static partial Regex AdministratorRegex();
}

internal record DiscordConfigCategory
{
	public string Name { get; set; } = "";
	public DiscordConfigChannel[] Channels { get; set; } = Array.Empty<DiscordConfigChannel>();
	public DiscordConfigPermissions Permissions { get; set; } = new();

	public override string ToString() => Name;
}

internal record DiscordConfigChannel
{
	public string Name { get; set; } = "";
	public DiscordConfigPermissions Permissions { get; set; } = new();
	public bool Voice { get; set; } = false;
	public string[]? Messages { get; set; } = null;
	public int Limit { get; set; } = -1;

	public override string ToString() => Name;
}

internal class DiscordConfigPermissions
{
	public string[] Deny { get; set; } = new[] { "*" };
	public string[] Read { get; set; } = new[] { "linked" };
	public string[] Write { get; set; } = Array.Empty<string>();
	public string[] Admin { get; set; } = new[] { "administrator", "Bots" };
}