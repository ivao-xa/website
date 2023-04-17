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

	internal readonly DiscordSocketClient _client = new(new DiscordSocketConfig() { GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.GuildMembers, MessageCacheSize = 30 });
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
				_ = SendMessageAsync(component.User, $"{component.User.Mention}, go to {reglink} to sign in through the website and get your discord roles!", 60000);
			}
			catch
			{
				_ = SendMessageAsync(component.Channel.Id, $"{component.User.Mention}, go to {reglink} to sign in through the website and get your discord roles!", 5000);
			}
		}

		_client.ButtonExecuted += buttonClicked;

		await foreach (var msgs in verifyChannel.GetMessagesAsync())
			await verifyChannel.DeleteMessagesAsync(msgs);

		await verifyChannel.SendMessageAsync(text: "Click the button to receive a message with instructions on how to connect to the website and receive your roles.", components: new ComponentBuilder().WithButton("Connect", "connect").Build());
	}

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

	private async Task Enshrine(string message)
	{
		if (FindTextChannelByName("museum-of-fail") is not SocketTextChannel museum)
			return;

		string[] sarkySalutations = new[] { "Hark", "Behold" };

		await museum.SendMessageAsync($"{sarkySalutations[Random.Shared.Next(sarkySalutations.Length)]}! {message} on this day, {DateTime.UtcNow:MMMM} {DateTime.UtcNow.Day} at {DateTime.UtcNow.ToLongTimeString()} in the year of our Lord {DateTime.UtcNow.Year}.");
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
					var examContext = await _webContextFactory.CreateDbContextAsync();
					var examTrainer = examContext.Users.FirstOrDefault(u => u.Snowflake == ((SocketGuildUser)command.User).Id);

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

					var assignedTrainings = examContext.TrainingRequests.Where(tr => tr.Trainer == examTrainer.Vid).AsNoTracking().ToArray();
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

					await examContext.Exams.AddAsync(new() {
						Rating = req.AtcRating.Value,
						Trainer = req.Trainer!.Value,
						Trainee = req.Trainee,
						Mock = false,
						Start = startTime,
						Position = (string)getOption("position").Value
					});

					await command.ModifyOriginalResponseAsync(r => r.Content = "Done! It's been scheduled.");
					break;

				case "unlink":
					var ivao = _client.Guilds.Single();
					IEnumerable<SocketRole> roleToSnowflakes(DiscordRoles roles, AtcRating? atcRating = null, PilotRating? pilotRating = null)
					{
						yield return ivao.Roles.Single(r => r.Name.Equals("linked", StringComparison.InvariantCultureIgnoreCase));

						for (int shift = 0; shift < 64; ++shift)
							if (roles.HasFlag((DiscordRoles)((ulong)1 << shift)))
								yield return ivao.Roles.Single(r => r.Name.Equals(_roles[(DiscordRoles)((ulong)1 << shift)], StringComparison.InvariantCulture));

						yield return ivao.Roles.Single(r => r.Name.Equals("visitor", StringComparison.InvariantCulture));

						foreach (var ar in Enum.GetValues<AtcRating>())
							yield return ivao.Roles.Single(r => r.Name.Equals(Enum.GetName((AtcRating)Math.Min((int)ar, (int)AtcRating.SEC)) switch { "SEC" => "SEC+", string a => a, _ => throw new Exception() }, StringComparison.InvariantCulture));

						foreach (var pr in Enum.GetValues<PilotRating>())
							yield return ivao.Roles.Single(r => r.Name.Equals(Enum.GetName((PilotRating)Math.Min((int)pr, (int)PilotRating.ATP)) switch { "ATP" => "ATP+", string a => a, _ => throw new Exception() }, StringComparison.InvariantCulture));
					}

					var unlinkContext = await _webContextFactory.CreateDbContextAsync();
					var unlinkUser = (SocketGuildUser)getOption("user").Value;
					if (await unlinkContext.Users.FirstOrDefaultAsync(u => u.Snowflake == command.User.Id) is not User executor || !executor.Roles.HasFlag(DiscordRoles.Administrator))
					{
						await command.ModifyOriginalResponseAsync(r => r.Content = "You don't have the authority to unlink a user. Ping an administrator.");
						break;
					}

					if (await unlinkContext.Users.FirstOrDefaultAsync(u => u.Snowflake == unlinkUser.Id) is User ulU)
					{
						unlinkContext.Users.Remove(ulU);
						await unlinkContext.SaveChangesAsync();
						_ = Enshrine($"{ulU.Name} ({ulU.Vid}/{ulU.Mention}) was unlinked by {executor.Name}");
					}

					_ = unlinkUser.RemoveRolesAsync(roleToSnowflakes(DiscordRoles.All).Distinct());
					await command.ModifyOriginalResponseAsync(r => r.Content = "Done! They'll now have to reverify.");
					break;

				case "nick":
					if (getOption("user").Value is not SocketGuildUser sgu || getOption("nick").Value is not string nick)
						break;

					var nickContext = await _webContextFactory.CreateDbContextAsync();
					if (nickContext.Users.SingleOrDefault(u => u.Snowflake == command.User.Id) is not User nickAdmin || !nickAdmin.Roles.HasFlag(DiscordRoles.Membership))
					{
						await command.ModifyOriginalResponseAsync(r => r.Content = "You don't have the necessary permissions.");
						break;
					}

					if (nickContext.Users.SingleOrDefault(u => u.Snowflake == sgu.Id) is not User u)
						break;

					u.Nickname = nick;
					_ = nickContext.SaveChangesAsync();
					await command.ModifyOriginalResponseAsync(r => r.Content = "All done!");
					break;

				default:
					throw new NotImplementedException("Unrecognised Command");
			}
		};

		_client.UserBanned += async (user, ivao) =>
		{
			await Enshrine($"{user.Mention} was BANNED");
		};

		_client.MessageDeleted += async (message, channel) =>
		{
			if (!message.HasValue || message.Value.Author is not SocketGuildUser sgu || !channel.HasValue || channel.Value.Name.Contains("museum"))
				return;

			await Enshrine($"In a spendid display of idiocy, {sgu.Nickname} sent a message (`{message.Value.Content}`) in <#{channel.Value.Id}> which was fit only for deletion");
		};

		_client.ReactionAdded += async (message, channel, reaction) =>
		{
			if (!message.HasValue || !reaction.User.IsSpecified || message.Value.Author is not SocketGuildUser victim)
				return;

			var context = await _webContextFactory.CreateDbContextAsync();
			if (context.Users.SingleOrDefault(u => u.Snowflake == reaction.User.Value.Id) is not User admin || !admin.Roles.HasFlag(DiscordRoles.Staff))
				return;

			switch (reaction.Emote.Name)
			{
				case "sandbag1":
					_ = victim.SetTimeOutAsync(TimeSpan.FromSeconds(10));
					break;

				case "sandbag2":
					_ = victim.SetTimeOutAsync(TimeSpan.FromHours(1));
					await Enshrine($"{victim.Mention} was timed out for one hour by {admin.Name}");
					break;

				case "sandbag3":
					_ = victim.SetTimeOutAsync(TimeSpan.FromDays(1));
					await Enshrine($"{victim.Mention} was timed out for a whole day by {admin.Name}");
					break;
			}
		};

		_client.Ready += async () =>
		{
			_client.PurgeUserCache();
			await EnforceDiscordConfigAsync();
			await _client.DownloadUsersAsync(_client.Guilds);
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

						if (ivao.Users.Count < 100)
						{
							_client.PurgeUserCache();
							await _client.DownloadUsersAsync(_client.Guilds);
							ivao = _client.Guilds.Single();
						}

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

			await ivao.CreateApplicationCommandAsync(new SlashCommandBuilder()
#if DEBUG
				.WithName("debug-unlink")
#else
				.WithName("unlink")
#endif
				.WithDescription("Unlink a user and force them to reverify.")
				.AddOption("user", ApplicationCommandOptionType.User, "The user to unlink", true)
				.Build());

			await ivao.CreateApplicationCommandAsync(new SlashCommandBuilder()
#if DEBUG
				.WithName("debug-nick")
#else
				.WithName("nick")
#endif
				.WithDescription("Nicknames a user.")
				.AddOption("user", ApplicationCommandOptionType.User, "The user to nickname", true)
				.AddOption("nick", ApplicationCommandOptionType.String, "The user's new nickname", true)
				.Build());

			await RequestSnowflakeAsync();
		};

		await _client.LoginAsync(TokenType.Bot, token);
		await _client.StartAsync();

		await Task.Delay(-1);
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

		foreach (var ex in context.Exams.ToArray().Where(ex => ex.Start.AddHours(-1) < DateTime.UtcNow && DateTime.UtcNow < ex.End.AddHours(1)))
		{
			var trainee = await context.Users.FindAsync(ex.Trainee);
			var trainer = await context.Users.FindAsync(ex.Trainer);

			if (trainee is null || trainer is null || trainee.Snowflake is null || trainer.Snowflake is null)
				continue;

			string channelName = $"{trainee.Name}'s {ex.Name}";
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

			string[] controllerSnowflakes = @event.Controllers.Split(':', StringSplitOptions.RemoveEmptyEntries).Select(vid => context.Users.Find(int.Parse(vid))?.Snowflake?.ToString()).Where(v => v is not null).Cast<string>().ToArray();
			await ivao.CreateVoiceChannelAsync(channelName, tcp => { tcp.CategoryId = new(category.Id); tcp.PermissionOverwrites = new(GetOverwrites(ivao, new() { Write = controllerSnowflakes })); });
		}

		foreach (var deletedChannel in knownChannels)
			await (FindVoiceChannelByName(deletedChannel)?.DeleteAsync() ?? Task.CompletedTask);
	}
}