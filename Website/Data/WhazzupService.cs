using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;

using Org.BouncyCastle.Utilities;

using System.Text.Json;

namespace Website.Data;

public class WhazzupService
{
	public WhazzupService(HttpClient httpClient, IDbContextFactory<WebsiteContext> webContextFactory)
	{
		_http = httpClient;
		_http.Timeout = TimeSpan.FromSeconds(5);

		Task.Run(async () =>
		{
			while (true)
			{
				await UpdateLastConnectedTimes(webContextFactory.CreateDbContext());
				await Task.Delay(TimeSpan.FromSeconds(15));
			}
		});
	}

	private readonly HttpClient _http;
	private Feed? _feed = null;
	private DateTime _feedUpdated = DateTime.MinValue;

	public async Task<Feed?> GetFeedAsync()
	{
		if (_feed is not null && DateTime.Now - _feedUpdated < TimeSpan.FromSeconds(15))
			return _feed.Value;

		try
		{
			_feed = await _http.GetFromJsonAsync<Feed>(@"https://api.ivao.aero/v2/tracker/whazzup", new JsonSerializerOptions(JsonSerializerDefaults.Web));
			_feedUpdated = DateTime.Now;
			return _feed;
		}
		catch
		{
			return null;
		}
	}

	public static bool IsXAPosition(string callsign) => callsign[0] is 'K' or 'C' or 'P' || callsign[..1] is "MY" or "NS" or "TJ" or "TI";

	public async Task UpdateLastConnectedTimes(WebsiteContext db)
	{
		Feed? _feed = await GetFeedAsync();
		if (_feed is not null)
		{
			var userVids = db.Users.AsNoTracking().Select(u => u.Vid).ToHashSet();
			foreach (var controller in _feed.Value.Clients.Atcs.Where(a => IsXAPosition(a.Callsign)))
			{
				User newUser = new() { Vid = controller.UserId, LastControlTime = _feed.Value.UpdatedAt };

				if (userVids.Contains(newUser.Vid))
					db.Users.Update(newUser);
				else
					db.Users.Add(newUser);
			}

			await db.SaveChangesAsync();
		}
	}
}

public struct Feed
{
	public DateTime UpdatedAt { get; set; }
	public Server[] Servers { get; set; }
	public Server[] VoiceServers { get; set; }
	public Clients Clients { get; set; }
	public Connections Connections { get; set; }
}

public struct Clients
{
	public Pilot[] Pilots { get; set; }
	public ATC[] Atcs { get; set; }
	public object[] FollowMe { get; set; }
	public Observer[] Observers { get; set; }
}

public struct Connections
{
	public int Total { get; set; }
	public int Supervisor { get; set; }
	public int Atc { get; set; }
	public int Observer { get; set; }
	public int Pilot { get; set; }
	public int WorldTour { get; set; }
}

public struct Server
{
	public string Id { get; set; }
	public string Hostname { get; set; }
	public string Ip { get; set; }
	public string Description { get; set; }
	public string CountryId { get; set; }
	public int CurrentConnections { get; set; }
	public int MaximumConnections { get; set; }
}

public struct Pilot
{
	public int Time { get; set; }
	public int Id { get; set; }
	public int UserId { get; set; }
	public string Callsign { get; set; }
	public string ServerId { get; set; }
	public string SoftwareTypeId { get; set; }
	public string SoftwareVersion { get; set; }
	public int Rating { get; set; }
	public DateTime CreatedAt { get; set; }
	public FlightPlan FlightPlan { get; set; }
	public PilotSession PilotSession { get; set; }
	public PilotTrack? LastTrack { get; set; }
}

public struct FlightPlan
{
	public int Id { get; set; }
	public int Revision { get; set; }
	public string AircraftId { get; set; }
	public int AircraftNumber { get; set; }
	public string DepartureId { get; set; }
	public string ArrivalId { get; set; }
	public string? AlternativeId { get; set; }
	public string? Alternative2Id { get; set; }
	public string Route { get; set; }
	public string Remarks { get; set; }
	public string Speed { get; set; }
	public string Level { get; set; }
	public char FlightRules { get; set; }
	public char FlightType { get; set; }
	public int EET { get; set; }
	public int Endurance { get; set; }
	public int DepartureTime { get; set; }
	public int? ActualDepartureTime { get; set; }
	public int PeopleOnBoard { get; set; }
	public DateTime CreatedAt { get; set; }
	public DateTime UpdatedAt { get; set; }
	public string AircraftEquipments { get; set; }
	public string AircraftTransponderTypes { get; set; }
	public Aircraft? Aircraft { get; set; }
}

public struct Aircraft
{
	public string IcaoCode { get; set; }
	public string Model { get; set; }
	public char WakeTurbulence { get; set; }
	public bool? IsMilitary { get; set; }
	public string Description { get; set; }
}

public struct PilotSession
{
	public string SimulatorId { get; set; }
}

public struct PilotTrack
{
	public int Altitude { get; set; }
	public int AltitudeDifference { get; set; }
	public decimal? ArrivalDistance { get; set; }
	public decimal? DepartureDistance { get; set; }
	public int GroundSpeed { get; set; }
	public int Heading { get; set; }
	public decimal Latitude { get; set; }
	public decimal Longitude { get; set; }
	public bool OnGround { get; set; }
	public string State { get; set; }
	public int Time { get; set; }
	public DateTime Timestamp { get; set; }
	public int Transponder { get; set; }
	public char TransponderMode { get; set; }
}

public struct ATC
{
	public int Time { get; set; }
	public int Id { get; set; }
	public int UserId { get; set; }
	public string Callsign { get; set; }
	public string ServerId { get; set; }
	public string SoftwareTypeId { get; set; }
	public string SoftwareVersion { get; set; }
	public int Rating { get; set; }
	public DateTime CreatedAt { get; set; }
	public AtcSession AtcSession { get; set; }
	public Atis? Atis { get; set; }
	public object LastTrack { get; set; }
}

public struct AtcSession
{
	public decimal Frequency { get; set; }
	public string Position { get; set; }
}

public struct Atis
{
	public string[] Lines { get; set; }
	public string Revision { get; set; }
	public DateTime Timestamp { get; set; }
}

public struct Observer
{
	public int Time { get; set; }
	public int Id { get; set; }
	public int UserId { get; set; }
	public string Callsign { get; set; }
	public string ServerId { get; set; }
	public string SoftwareTypeId { get; set; }
	public string SoftwareVersion { get; set; }
	public DateTime CreatedAt { get; set; }
	public AtcSession AtcSession { get; set; }
	public object LastTrack { get; set; }
}