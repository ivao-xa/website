namespace Website.Data;

using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.EntityFrameworkCore;

using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

public class IvaoLoginService
{
	private readonly ProtectedSessionStorage _session;
	private readonly HttpClient _http;
	private readonly IDbContextFactory<WebsiteContext> _dbContextFactory;

	public IvaoLoginService(ProtectedSessionStorage session, HttpClient http, IDbContextFactory<WebsiteContext> dbContextFactory) =>
		(_session, _http, _dbContextFactory) = (session, http, dbContextFactory);

	public async Task RegisterUserAsync(string token)
	{
		JsonSerializerOptions options = new() { NumberHandling = JsonNumberHandling.AllowReadingFromString, PropertyNameCaseInsensitive = true };
		IvaoLoginData? json = await _http.GetFromJsonAsync<IvaoLoginData>($"https://login.ivao.aero/api.php?type=json&token={token}", options);

		if (json is null)
			return;

		var db = await _dbContextFactory.CreateDbContextAsync();
		if (await db.Users.FindAsync(json.Vid) is User u)
		{
			u.FirstName ??= json.FirstName;
			u.LastName = json.LastName;
			u.RatingAtc = (AtcRating)json.RatingAtc;
			u.RatingPilot = (PilotRating)json.RatingPilot;
			u.Division = json.Division;
			u.Country = json.Country;
			u.Staff = string.Join(':', json.Staff.Split(':', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));

			await _session.SetAsync("User", u);
		}
		else
		{
			User user = new()
			{
				Vid = json.Vid,
				FirstName = json.FirstName,
				LastName = json.LastName,
				RatingAtc = (AtcRating)json.RatingAtc,
				RatingPilot = (PilotRating)json.RatingPilot,
				Division = json.Division,
				Country = json.Country,
				Staff = string.Join(':', json.Staff.Split(':', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)),
				FaaChecked = json.Country != "CA",
				NavCanChecked = json.Country == "CA"
			};
			await db.Users.AddAsync(user);
		}

		await db.SaveChangesAsync();
	}

	public async Task<User?> GetAuthenticatedUserAsync()
	{
		try
		{
			ProtectedBrowserStorageResult<User> result = await _session.GetAsync<User>("User");

			return result.Success ? result.Value! : null;
		}
		catch (CryptographicException)
		{
			await _session.DeleteAsync("User");
			return null;
		}
	}
}

public record IvaoLoginData(int Result, int Vid, string FirstName, string LastName, int Rating, int RatingAtc, int RatingPilot, string Division, string Country, int Hours_Atc, int Hours_Pilot, string Staff) { }