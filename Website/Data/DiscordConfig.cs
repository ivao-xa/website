using Discord;
using Discord.WebSocket;

using System.Text.Json;
using System.Text.RegularExpressions;

namespace Website.Data;

public partial class DiscordService
{

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
		var oldServerRoles = ivao.Roles.ToArray();
		foreach ((string Name, uint? Color) in roles)
		{
			if (!oldServerRoles.Any(r => r.Name == Name.TrimEnd("*^".ToCharArray())))
				// Add missing role
				_ = ivao.CreateRoleAsync(
					Name.TrimEnd("*^".ToCharArray()),
					Name.EndsWith('*') ? new GuildPermissions(administrator: true) : GuildPermissions.None,
					Color is null ? null : new Color(Color.Value),
					Name.EndsWith('*') || Name.EndsWith('^'),
					true
				);
			else
				// Update existing role
				_ = oldServerRoles.Single(r => r.Name == Name.TrimEnd("*^".ToCharArray())).ModifyAsync(rp =>
				{
					rp.Permissions = new(Name.EndsWith('*') ? new GuildPermissions(administrator: true) : GuildPermissions.None);
					rp.Color = Color is null ? new() : new(new Color(Color.Value));
					rp.Hoist = Name.EndsWith('*') || Name.EndsWith('^');
					rp.Mentionable = true;
				});
		}

		DiscordConfigCategory[] config = JsonSerializer.Deserialize<DiscordConfigCategory[]>(File.ReadAllText(CHANNEL_CONF_PATH)) ?? throw new Exception("Invalid Discord config.");

		// Delete any unwanted categories.
#if !DEBUG
		while (ivao.CategoryChannels.Any(c => !config.Any(cc => c.Name == cc.Name)))
			await (ivao.GetCategoryChannel(ivao.CategoryChannels.First(c => !config.Any(cc => c.Name == cc.Name)).Id)?.DeleteAsync() ?? Task.CompletedTask);
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

			if (ivao.Users.Count < 100)
			{
				_client.PurgeUserCache();
				await _client.DownloadUsersAsync(_client.Guilds);
				ivao = _client.Guilds.Single();
			}

			// Enforce category permissions
			await scc.ModifyAsync(gcp => gcp.PermissionOverwrites = new(GetOverwrites(ivao, category.Permissions)));

			// Delete any unwanted channels
			string[] catNames = category.Channels.Select(cc => cc.Name).ToArray();
			foreach (var channel in scc.Channels.Where(c => !catNames.Contains(c.Name) || c is SocketTextChannel && category.Channels.First(cc => cc.Name == c.Name).Voice || c is SocketVoiceChannel && !category.Channels.First(cc => cc.Name == c.Name).Voice))
				_ = channel.DeleteAsync();

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
					_ = svc.ModifyAsync(acp => { acp.PermissionOverwrites = new(GetOverwrites(ivao, channel.Permissions)); acp.CategoryId = new(scc.Id); if (channel.Limit > 0) acp.UserLimit = new(channel.Limit); });
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

					Task tmp = stc.ModifyAsync(tcp => { tcp.PermissionOverwrites = new(GetOverwrites(ivao, channel.Permissions)); tcp.CategoryId = new(scc.Id); });

					if (channel.Messages is not null)
					{
						await tmp;

						await foreach (var msgs in stc.GetMessagesAsync())
							_ = stc.DeleteMessagesAsync(msgs);

						foreach (string msg in channel.Messages)
							await stc.SendMessageAsync(msg);
					}
				}
		}
	}

	/// <summary>Enforces the correct roles for a given <see cref="User"/>.</summary>
	public async Task<DiscordRoles> EnforceRolesAsync(User user, IvaoApiService api)
	{
		var ivao = _client.Guilds.Single();

		/// <summary>Helper function to enable toggeling bit flags.</summary>
		static void setFlag(User user, bool set, DiscordRoles flag)
		{
			if (set)
				user.Roles |= flag;
			else
				user.Roles &= ~flag;
		}

		// Sets staff type roles.
		setFlag(user, user.Staff is not null && TrainerRegex().IsMatch(user.Staff), DiscordRoles.Training);
		setFlag(user, user.Staff is not null && MembershipRegex().IsMatch(user.Staff), DiscordRoles.Membership);
		setFlag(user, user.Staff is not null && EventsRegex().IsMatch(user.Staff), DiscordRoles.Events);
		setFlag(user, user.Staff is not null && AdministratorRegex().IsMatch(user.Staff), DiscordRoles.Administrator);

		// Sets membership and staff status.
		bool isMember = (await api.GetCountriesAsync()).SelectMany(c => new[] { c.id, c.divisionId }).Contains(user.Division);
		setFlag(user, isMember && !string.IsNullOrEmpty(user.Staff), DiscordRoles.Staff);
		setFlag(user, isMember, DiscordRoles.Member);

		if (user.Snowflake is null)
			return user.Roles;

		/// <summary>Converts internal role representations to API-friendly sets of role snowflakes.</summary>
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
			return user.Roles;

#if DEBUG
		await igu.AddRolesAsync(roleToSnowflakes(user.Roles, user.RatingAtc, user.RatingPilot));
#else
		await igu.RemoveRolesAsync(igu.RoleIds);

		/// <summary>A helper function to order a user's staff positions logically.</summary>
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
				gup.Nickname = new($"{user.Name} | {user.Vid}");
			else
				gup.Nickname = new($"{user.Name} | {staffPos(user.Staff.Split(':'))}");
		});
#endif

		return user.Roles;
	}

	/// <summary>Enforces roles for all users in a the default server.</summary>
	public async Task EnforceAllRolesAsync(WebsiteContext db, IvaoApiService api)
	{
		// Get the default server and ensure a current user list.
		var ivao = _client.Guilds.Single();
		_client.PurgeUserCache();
		await _client.DownloadUsersAsync(new[] { ivao });

		foreach (var user in db.Users.Where(u => u.Snowflake != null)
#if DEBUG
			.Where(u => u.Roles.HasFlag(DiscordRoles.Administrator))
#endif
			)
			user.Roles = await EnforceRolesAsync(user, api);

		await db.SaveChangesAsync();
	}

	/// <summary>Searches for a given text channel by name.</summary>
	private SocketTextChannel? FindTextChannelByName(string channelName) =>
		_client.Guilds.SelectMany(g => g.Channels.Where(c => c is SocketTextChannel))
#if DEBUG
		.FirstOrDefault(c => c.Name.Equals("test-" + channelName, StringComparison.InvariantCultureIgnoreCase))
#else
		.FirstOrDefault(c => c.Name == channelName)
#endif
		as SocketTextChannel;

	/// <summary>Searches for a given voice channel by name.</summary>
	private SocketVoiceChannel? FindVoiceChannelByName(string channelName) =>
		_client.Guilds.SelectMany(g => g.Channels.Where(c => c is SocketVoiceChannel))
#if DEBUG
		.FirstOrDefault(c => c.Name == "[TEST] " + channelName)
#else
		.FirstOrDefault(c => c.Name == channelName)
#endif
		as SocketVoiceChannel;

	/// <summary>Searches for a given category by name.</summary>
	private SocketCategoryChannel? FindCategoryByName(string categoryName) =>
		_client.Guilds.SelectMany(g => g.CategoryChannels)
#if DEBUG
		.FirstOrDefault(c => c.Name == (categoryName.StartsWith('[') ? categoryName : $"[-BOT-{categoryName}-]"));
#else
		.FirstOrDefault(c => c.Name == (categoryName.StartsWith('[') ? categoryName : $"[-{categoryName}-]"));
#endif

	/// <summary>
	/// Returns a list of Discord <see cref="Overwrite">Overwrites</see> representing the given <see cref="DiscordConfigPermissions"/>.
	/// </summary>
	/// <param name="ivao">The server for which the overwrites should be valid.</param>
	/// <param name="perms">The permissions to represent.</param>
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

		foreach (var rp in perms.Read.Where(targets.ContainsKey))
			yield return new Overwrite(targets[rp].Id, targets[rp].Target, readPerms);

		foreach (var rp in perms.Write.Where(targets.ContainsKey))
			yield return new Overwrite(targets[rp].Id, targets[rp].Target, writePerms);

		foreach (var rp in perms.Admin.Where(targets.ContainsKey))
			yield return new Overwrite(targets[rp].Id, targets[rp].Target, adminPerms);
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

/// <summary>
/// A serializable representation of a Discord category.
/// </summary>
internal record DiscordConfigCategory
{
	public string Name { get; set; } = "";
	public DiscordConfigChannel[] Channels { get; set; } = Array.Empty<DiscordConfigChannel>();
	public DiscordConfigPermissions Permissions { get; set; } = new();

	public override string ToString() => Name;
}

/// <summary>
/// A serializable representation of a Discord channel.
/// </summary>
internal record DiscordConfigChannel
{
	public string Name { get; set; } = "";
	public DiscordConfigPermissions Permissions { get; set; } = new();
	public bool Voice { get; set; } = false;
	public string[]? Messages { get; set; } = null;
	public int Limit { get; set; } = -1;

	public override string ToString() => Name;
}

/// <summary>
/// Represents a channel or category's user permission levels.
/// </summary>
internal class DiscordConfigPermissions
{
	public string[] Deny { get; set; } = new[] { "*" };
	public string[] Read { get; set; } = new[] { "linked" };
	public string[] Write { get; set; } = Array.Empty<string>();
	public string[] Admin { get; set; } = new[] { "administrator", "Bots" };
}