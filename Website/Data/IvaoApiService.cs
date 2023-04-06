namespace Website.Data;

public class IvaoApiService
{
    public IvaoApiService(HttpClient httpClient, IConfiguration config)
    {
        _http = httpClient;
        _http.Timeout = TimeSpan.FromSeconds(5);
        _apiKey = config["ivao:apiKey"] ?? "";
    }

    private readonly string _apiKey;

    private readonly HttpClient _http;
    private HashSet<Fra>? _fras = null;
    private DateTime _frasUpdated = DateTime.MinValue;

    private static readonly SemaphoreSlim _updateSemaphore = new(1);

    public async Task<IEnumerable<Fra>?> GetFrasAsync(string division = "XA", bool vidBased = true, bool ratingBased = true)
    {
        await _updateSemaphore.WaitAsync();

        if (_fras is not null && DateTime.UtcNow - _frasUpdated < TimeSpan.FromMinutes(5))
        {
            _updateSemaphore.Release();
            return _fras.Where(f => (vidBased || f.userId is null) && (ratingBased || f.minAtc is null));
        }

        try
        {
            string queryUri = $"https://api.ivao.aero/v2/fras?page=1&perPage=100&divisionId={division}&isActive=true&members=true&positions=true&expand=true&apiKey={_apiKey}";
			var pg1 = await _http.GetFromJsonAsync<FraList>(queryUri);

            if (pg1 is null)
                return null;

            _fras = new();
            _fras.UnionWith(pg1.items);

            for (int page = 2; page <= pg1.pages; ++page)
            {
                var pg = await _http.GetFromJsonAsync<FraList>($"https://api.ivao.aero/v2/fras?page={page}&perPage=100&divisionId={division}&isActive=true&members=true&positions=true&expand=true&apiKey={_apiKey}");
                if (pg is null)
                    break;
                _fras.UnionWith(pg.items);
            }

            _frasUpdated = DateTime.UtcNow;
            return _fras.Where(f => (vidBased || f.userId is null) && (ratingBased || f.minAtc is null));
        }
        catch
        {
            return null;
        }
        finally
        {
            _updateSemaphore.Release();
        }
    }

    public async Task<Country[]> GetCountriesAsync(string division = "XA") =>
        (await _http.GetFromJsonAsync<Countries>($"https://api.ivao.aero/v2/countries?page=1&perPage=50&divisionId={division}&apiKey={_apiKey}"))!.items;

    public async Task<Center[]> GetCentersAsync(params string[] countries)
    {
        List<Center> result = new();
        foreach (string c in countries)
            result.AddRange((await _http.GetFromJsonAsync<Centers>($"https://api.ivao.aero/v2/centers?page=1&perPage=100&countryId={c}&apiKey={_apiKey}"))!.items);

        return result.ToArray();
    }
}

#pragma warning disable IDE1006
public class FraList
{
    public Fra[] items { get; set; } = Array.Empty<Fra>();
    public int totalItems { get; set; }
    public int perPage { get; set; }
    public int page { get; set; }
    public int pages { get; set; }
}

public class Fra
{
    public int id { get; set; }
    public int? userId { get; set; }
    public int? atcPositionId { get; set; }
    public int? subcenterId { get; set; }
    public string startTime { get; set; } = string.Empty;
    public string endTime { get; set; } = string.Empty;
    public bool? dayMon { get; set; }
    public bool? dayTue { get; set; }
    public bool? dayWed { get; set; }
    public bool? dayThu { get; set; }
    public bool? dayFri { get; set; }
    public bool? daySat { get; set; }
    public bool? daySun { get; set; }
    public string date { get; set; } = string.Empty;
    public int? minAtc { get; set; }
    public bool active { get; set; }
    public AtcPosition? atcPosition { get; set; }
    public Subcenter? subcenter { get; set; }

    public override int GetHashCode() => HashCode.Combine(userId, minAtc, atcPosition?.composePosition ?? subcenter!.composePosition);
}

public class Subcenter
{
    public int id { get; set; }
    public string centerId { get; set; } = string.Empty;
    public string atcCallsign { get; set; } = string.Empty;
    public string middleIdentifier { get; set; } = string.Empty;
    public string position { get; set; } = string.Empty;
    public string composePosition { get; set; } = string.Empty;
}

public class AtcPosition
{
    public int id { get; set; }
    public string airportId { get; set; } = string.Empty;
    public string atcCallsign { get; set; } = string.Empty;
    public string composePosition { get; set; } = string.Empty;
    public string? middleIdentifier { get; set; }
    public string position { get; set; } = string.Empty;
}


public class Countries
{
    public Country[] items { get; set; } = Array.Empty<Country>();
    public int totalItems { get; set; }
    public int perPage { get; set; }
    public int page { get; set; }
    public int pages { get; set; }
}

public class Country
{
    public string id { get; set; } = string.Empty;
    public string name { get; set; } = string.Empty;
    public string region { get; set; } = string.Empty;
    public string divisionId { get; set; } = string.Empty;
}

public class Centers
{
    public Center[] items { get; set; } = Array.Empty<Center>();
    public int totalItems { get; set; }
    public int perPage { get; set; }
    public int page { get; set; }
    public int pages { get; set; }
}

public class Center
{
    public string id { get; set; } = string.Empty;
    public string name { get; set; } = string.Empty;
    public string countryId { get; set; } = string.Empty;
    public bool military { get; set; }
}
#pragma warning restore IDE1006