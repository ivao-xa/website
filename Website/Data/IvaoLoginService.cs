namespace Website.Data;

using Discord;

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

    /// <summary>Pulls the user's info from the IVAO login API, generates a <see cref="User"/>, and pushes it to the database.</summary>
    /// <param name="token">The user's IVAO login token.</param>
    /// <returns>The created/retrieved <see cref="User"/>.</returns>
    public async Task<User?> RegisterUserAsync(string token)
    {
        // Retrieve user's data from the IVAO API.
        JsonSerializerOptions options = new() { NumberHandling = JsonNumberHandling.AllowReadingFromString, PropertyNameCaseInsensitive = true };
        if (await _http.GetFromJsonAsync<IvaoLoginData>($"https://login.ivao.aero/api.php?type=json&token={token}", options) is not IvaoLoginData json)
            return null;

        User retval;

        // Generate or update the user's database entry.
        var db = await _dbContextFactory.CreateDbContextAsync();
        if (!(json.Rating is 2 or 11 or 12))
        {
            // User is not active.
            if (await db.Users.FindAsync(json.Vid) is User suspendedUser)
            {
                db.Users.Remove(suspendedUser);
				Console.WriteLine($"Deleted suspended user {suspendedUser.Vid}  ({suspendedUser.Name} {suspendedUser.LastName})");
				await db.SaveChangesAsync();
            }

            return null;
        }

        if (await db.Users.FindAsync(json.Vid) is User u)
        {
            // Already an entry. Update it.
            u.FirstName ??= json.FirstName;
            u.LastName = json.LastName;
            u.RatingAtc = (AtcRating)json.RatingAtc;
            u.RatingPilot = (PilotRating)json.RatingPilot;
            u.Division = json.Division;
            u.Country = json.Country;
            u.Staff = json.Staff is null ? null : string.Join(':', json.Staff.Split(':', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));

            await _session.SetAsync("User", u);
			Console.WriteLine($"Updated user {u.Vid} ({u.Name} {u.LastName})");
			retval = u;
		}
		else
		{
			// First time logging in. Add them.
			User user = new() {
                Vid = json.Vid,
                FirstName = json.FirstName,
                LastName = json.LastName,
                RatingAtc = (AtcRating)json.RatingAtc,
                RatingPilot = (PilotRating)json.RatingPilot,
                Division = json.Division,
                Country = json.Country,
                Staff = json.Staff is null ? null : string.Join(':', json.Staff.Split(':', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)),
                FaaChecked = new[] { "US", "XA" }.Contains(json.Country),
                NavCanChecked = json.Country == "CA"
            };

            await db.Users.AddAsync(user);
			Console.WriteLine($"Added user {user.Vid} ({user.Name} {user.LastName})");
            retval = user;
        }

        _ = db.SaveChangesAsync();
        return retval;
    }

    /// <summary>Gets the current <see cref="User"/> from the browser's cache.</summary>
    /// <returns>The current <see cref="User"/> if they are signed in, otherwise <see langword="null"/>.</returns>
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
        catch (TaskCanceledException)
        {
            return null;
        }
    }
}

public record IvaoLoginData(int Result, int Vid, string FirstName, string LastName, int Rating, int RatingAtc, int RatingPilot, string Division, string Country, int Hours_Atc, int Hours_Pilot, string Staff) { }