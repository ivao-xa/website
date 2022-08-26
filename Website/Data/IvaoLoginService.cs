namespace Website.Data;

using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

using System.Security.Cryptography;

public class IvaoLoginService
{
	private readonly ProtectedSessionStorage _session;
	private readonly HttpClient _http;

	public IvaoLoginService(ProtectedSessionStorage session, HttpClient http) =>
		(_session, _http) = (session, http);

	public async Task RegisterUserAsync(string token)
	{
		IvaoLoginData? json = await _http.GetFromJsonAsync<IvaoLoginData>($"https://login.ivao.aero/api.php?type=json&token={token}");

		if (json is null)
			return;

		await _session.SetAsync("User", json);
	}

	public async Task<IvaoLoginData?> GetAuthenticatedUserAsync()
	{
		try
		{
			ProtectedBrowserStorageResult<IvaoLoginData> result = await _session.GetAsync<IvaoLoginData>("User");

			return result.Success ? result.Value! : null;
		}
		catch (CryptographicException)
		{
			await _session.DeleteAsync("User");
			return null;
		}
	}
}

public record IvaoLoginData(int Result, string Vid, string FirstName, string LastName, int Rating, int RatingAtc, int RatingPilot, string Division, string Country, int Hours_Atc, int Hours_Pilot, string Staff) { }