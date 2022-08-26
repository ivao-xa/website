using Discord;
using Discord.Rest;
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

	private SocketTextChannel? FindChannelByName(string channelName) =>
		_client.Guilds.SelectMany(g => g.Channels.Where(c => c is SocketTextChannel)).FirstOrDefault(c => c.Name == channelName) as SocketTextChannel;

	private async Task LaunchAsync(string token, WhazzupService whazzup)
	{
		Dictionary<int, ulong> trackedMessages = new();

		_client.Log += LogAsync;
		_client.Ready += () =>
		{
			if (FindChannelByName("bot-log") is SocketTextChannel channel)
			{
				whazzup.AtcConnected += async controller => trackedMessages.Add(controller.UserId, (await channel.SendMessageAsync(text: $"{controller.Callsign} is online!")).Id);
				whazzup.AtcDisconnected += async controller => { await channel.DeleteMessageAsync(trackedMessages[controller.UserId]); trackedMessages.Remove(controller.UserId); };
			}

			return Task.CompletedTask;
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
