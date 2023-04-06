using System.Collections.Concurrent;

namespace Website.Data.Ocms;

public record DatalinkUpdateRequest((string Fix, Time Time) Passed, int FlightLevel, decimal MachNumber, (string Fix, Time Time) Estimating, string Next) { }

public record struct DatalinkUpdateResponse(bool Approved)
{
	public string? RejectionReason { get; init; }
}

public class DatalinkService
{
	public event Action<string, DatalinkUpdateRequest>? UpdateRequested;
	public event Action<string, DatalinkUpdateResponse>? ResponseIssued;

	private readonly ConcurrentDictionary<string, byte> _connectedPilots = new();

	public void RegisterPilot(string callsign) =>
		_connectedPilots.TryAdd(callsign, 0);

	public void UnregisterPilot(string callsign) =>
		_connectedPilots.TryRemove(callsign, out _);

	public bool IsConnected(string pilot) =>
		_connectedPilots.ContainsKey(pilot);

	public void Request(string callsign, DatalinkUpdateRequest request) =>
		UpdateRequested?.Invoke(callsign, request);

	public void Reply(string callsign, DatalinkUpdateResponse response) =>
		ResponseIssued?.Invoke(callsign, response);
}