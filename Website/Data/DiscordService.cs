using Discord;
using Discord.WebSocket;

using Microsoft.EntityFrameworkCore;

using System.Text.Json;
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
        if (FindTextChannelByName(channelName) is not SocketTextChannel stc)
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

        await Task.Run(() => mres.WaitOne());
        return result;
    }

    private readonly Dictionary<int, ulong> _snowflakingIds = new();
    private readonly SemaphoreSlim _snowflakingSemaphore = new(1);
    public async Task<ulong> RequestSnowflakeAsync(string channel, User user)
    {
        if (FindTextChannelByName(channel) is not SocketTextChannel stc)
            throw new Exception();

        return await RequestSnowflakeAsync(stc.Id, user);
    }

    public async Task<ulong> RequestSnowflakeAsync(ulong channelId, User user)
    {
        if (_snowflakingIds.ContainsKey(user.Vid))
            return _snowflakingIds[user.Vid];

        try
        {
            await _snowflakingSemaphore.WaitAsync();
            if (_snowflakingIds.ContainsKey(user.Vid))
                return _snowflakingIds[user.Vid];

            if (await _client.GetChannelAsync(channelId) is not SocketTextChannel stc)
                throw new ArgumentException("Unknown botlog id", nameof(channelId));

            AutoResetEvent mres = new(false);
            ulong snowflake = 0;
            async Task buttonClicked(SocketMessageComponent component)
            {
                _client.ButtonExecuted -= buttonClicked;
                snowflake = component.User.Id;
                await component.Message.DeleteAsync();
                mres.Set();
            }

            _client.ButtonExecuted += buttonClicked;
            await stc.SendMessageAsync(text: $"{user.Mention} ({user.FirstName}), click the button to connect to the website.", components: new ComponentBuilder().WithButton("Me!", "me").Build());

            await Task.Run(() => mres.WaitOne());
            _snowflakingIds.Add(user.Vid, snowflake);
            return snowflake;
        }
        finally { _snowflakingSemaphore.Release(); }
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
        ivao = _client.Guilds.Single();

        var guildUser = ivao.GetUser(user.Snowflake.Value);
        if (guildUser is not IGuildUser igu)
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

        bool isMember = (await api.GetCountriesAsync()).SelectMany(c => new[] { c.id, c.divisionId }).Contains(user.Division);
        setFlag(isMember && !string.IsNullOrEmpty(user.Staff), DiscordRoles.Staff);
        setFlag(isMember, DiscordRoles.Member);

        await igu.AddRolesAsync(roleToSnowflakes(user.Roles).ToArray());
    }

    private SocketTextChannel? FindTextChannelByName(string channelName) =>
        _client.Guilds.SelectMany(g => g.Channels.Where(c => c is SocketTextChannel)).FirstOrDefault(c => c.Name == channelName) as SocketTextChannel;
    private SocketVoiceChannel? FindVoiceChannelByName(string channelName) =>
        _client.Guilds.SelectMany(g => g.Channels.Where(c => c is SocketVoiceChannel)).FirstOrDefault(c => c.Name == channelName) as SocketVoiceChannel;
    private SocketCategoryChannel? FindCategoryByName(string categoryName) =>
        _client.Guilds.SelectMany(g => g.CategoryChannels).FirstOrDefault(c => c.Name == categoryName);

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
        Dictionary<ATC, User?> trackedControllers = new();

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
            // Enforce botlog config
            const string CONF_PATH = "discord.json";

            if (!File.Exists(CONF_PATH))
                await File.WriteAllTextAsync(CONF_PATH, "[]");

            var ivao = _client.Guilds.Single();

            DiscordConfigCategory[] config = JsonSerializer.Deserialize<DiscordConfigCategory[]>(File.ReadAllText(CONF_PATH)) ?? throw new Exception("Invalid Discord config.");

            // Delete any unwanted categories.
            //while (ivao.CategoryChannels.Any(c => !config.Any(cc => c.Name == cc.Name)))
            //    await ivao.GetCategoryChannel(ivao.CategoryChannels.First(c => !config.Any(cc => c.Name == cc.Name)).Id)?.DeleteAsync();

            IEnumerable<Overwrite> getOverwrites(DiscordConfigPermissions perms)
            {
                OverwritePermissions readPerms = new(viewChannel: PermValue.Allow, connect: PermValue.Allow);
                OverwritePermissions writePerms = new(
                    viewChannel: PermValue.Allow, sendMessages: PermValue.Allow, sendMessagesInThreads: PermValue.Allow,
                    attachFiles: PermValue.Allow, addReactions: PermValue.Allow,
                    connect: PermValue.Allow, speak: PermValue.Allow
                );
                OverwritePermissions adminPerms = new(1 << 3, 0);

                Dictionary<string, (ulong Id, PermissionTarget Target)> targets = new();
                foreach (var p in perms.Read.Concat(perms.Write).Concat(perms.Admin))
                {
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
                }

                yield return new Overwrite(ivao.EveryoneRole.Id, PermissionTarget.Role, new(viewChannel: PermValue.Deny));

                foreach (var rp in perms.Read.Where(r => targets.ContainsKey(r)))
                    yield return new Overwrite(targets[rp].Id, targets[rp].Target, readPerms);

                foreach (var rp in perms.Write.Where(r => targets.ContainsKey(r)))
                    yield return new Overwrite(targets[rp].Id, targets[rp].Target, writePerms);

                foreach (var rp in perms.Admin.Where(r => targets.ContainsKey(r)))
                    yield return new Overwrite(targets[rp].Id, targets[rp].Target, adminPerms);
            }

            // Check all current categories
            foreach (var category in config)
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
                await scc.ModifyAsync(gcp => gcp.PermissionOverwrites = new(getOverwrites(category.Permissions)));

                // Delete any unwanted channels
                string[] catNames = category.Channels.Select(cc => cc.Name).ToArray();
                foreach (var channel in scc.Channels.Where(c => !catNames.Contains(c.Name) || (c is SocketTextChannel && category.Channels.First(cc => cc.Name == c.Name).Voice) || (c is SocketVoiceChannel && !category.Channels.First(cc => cc.Name == c.Name).Voice)))
                    await channel.DeleteAsync();

                foreach (var channel in category.Channels.Select(c => new DiscordConfigChannel() { Name = "_" + c.Name, Permissions = c.Permissions, Voice = c.Voice, Messages = c.Messages }))
                {
                    if (channel.Voice)
                    {
                        if (FindVoiceChannelByName(channel.Name) is not SocketVoiceChannel svc)
                        {
                            var rvc = await ivao.CreateVoiceChannelAsync(channel.Name, vcp => vcp.CategoryId = new(scc.Id));
                            do
                            {
                                svc = ivao.GetVoiceChannel(rvc.Id);
                            } while (svc is null);
                        }

                        await svc.SyncPermissionsAsync();
                        await svc.ModifyAsync(acp => { acp.PermissionOverwrites = new(getOverwrites(channel.Permissions)); acp.CategoryId = new(scc.Id); });
                    }
                    else
                    {
                        if (FindTextChannelByName(channel.Name) is not SocketTextChannel stc)
                        {
                            var rtc = await ivao.CreateTextChannelAsync(channel.Name, tcp => tcp.CategoryId = new(scc.Id));
                            do
                            {
                                stc = ivao.GetTextChannel(rtc.Id);
                            } while (stc is null);
                        }

                        await stc.ModifyAsync(tcp => { tcp.PermissionOverwrites = new(getOverwrites(channel.Permissions)); tcp.CategoryId = new(scc.Id); });

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

            // Running online controllers view
            if (FindCategoryByName("[-ONLINE-]") is SocketCategoryChannel onlineCategory)
            {
                SocketTextChannel? botlog = FindTextChannelByName("_active-controllers");

                ulong? trackingMessage = (await (botlog?.SendMessageAsync("Online controllers: None") ?? Task.FromResult<Discord.Rest.RestUserMessage?>(null)))?.Id;

                static Embed genEmbed(KeyValuePair<ATC, User?> user) => new EmbedBuilder().WithCurrentTimestamp().WithDescription($"[{user.Value?.Mention ?? user.Key.ToString()} Member Page](https://ivao.aero/member?Id={user.Key})").WithImageUrl($"https://status.ivao.aero/{user.Key}.png?time={DateTime.UtcNow.Ticks}").Build();
                async Task updateAsync()
                {
                    foreach (var c in onlineCategory.Channels.Where(c => c.Id != botlog?.Id && !trackedControllers.Any(v => v.Key.Callsign == c.Name)))
                        await c.DeleteAsync();

                    await (botlog?.ModifyMessageAsync(trackingMessage!.Value, mp => { mp.Content = "Online controllers:"; if (trackedControllers.Any()) mp.Embeds = new(trackedControllers.Select(genEmbed).ToArray()); else mp.Content += " None"; }) ?? Task.CompletedTask);
                }

                whazzup.AtcConnected += async controller =>
                {
                    var context = await _webContextFactory.CreateDbContextAsync();
                    trackedControllers.Add(controller, context.Users.AsNoTracking().First(u => u.Vid == controller.UserId));
                    await ivao.CreateVoiceChannelAsync(controller.Callsign, vcp => { vcp.CategoryId = onlineCategory.Id; vcp.PermissionOverwrites = new(getOverwrites(new() { Deny = new[] { "*" }, Read = Array.Empty<string>(), Write = new[] { "bot-member" }, Admin = new[] { "bot-administrator", "Bots" } })); });
                    await updateAsync();
                };

                whazzup.AtcDisconnected += async controller =>
                {
                    foreach (var c in trackedControllers.Where(tc => tc.Value.Vid == controller.UserId))
                        trackedControllers.Remove(c.Key);

                    await (FindVoiceChannelByName(controller.Callsign)?.DeleteAsync() ?? Task.CompletedTask);

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
                    finally { foreach (var c in onlineCategory.Channels) await c.DeleteAsync(); await onlineCategory.DeleteAsync(); }
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

internal class DiscordConfigCategory
{
    public string Name { get; set; } = "";
    public DiscordConfigChannel[] Channels { get; set; } = Array.Empty<DiscordConfigChannel>();
    public DiscordConfigPermissions Permissions { get; set; } = new();

    public override string ToString() => Name;
}

internal class DiscordConfigChannel
{
    public string Name { get; set; } = "";
    public DiscordConfigPermissions Permissions { get; set; } = new();
    public bool Voice { get; set; } = false;
    public string[]? Messages { get; set; } = null;

    public override string ToString() => Name;
}

internal class DiscordConfigPermissions
{
    public string[] Deny { get; set; } = new[] { "*" };
    public string[] Read { get; set; } = Array.Empty<string>();
    public string[] Write { get; set; } = new[] { "bot-member" };
    public string[] Admin { get; set; } = new[] { "bot-administrator", "Bots" };
}