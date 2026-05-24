using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using EmbyTMDBScraperFix.Configuration;

namespace EmbyTMDBScraperFix.Services;

public sealed class TmdbApiClient
{
    private readonly ProxyHttpClientService _http;
    private readonly PluginLogService _log;

    public TmdbApiClient(ProxyHttpClientService http, PluginLogService log)
    {
        _http = http;
        _log = log;
    }

    public Task<TmdbSearchResponse?> SearchMovieAsync(string name, int? year, PluginConfiguration cfg, CancellationToken ct)
    {
        var query = new Dictionary<string, string>
        {
            ["query"] = name,
            ["include_adult"] = cfg.EnableAdultMetadata ? "true" : "false"
        };
        if (year.HasValue) query["year"] = year.Value.ToString(CultureInfo.InvariantCulture);
        return GetJsonAsync<TmdbSearchResponse>("/search/movie", query, cfg, ct);
    }

    public Task<TmdbSearchResponse?> SearchSeriesAsync(string name, int? year, PluginConfiguration cfg, CancellationToken ct)
    {
        var query = new Dictionary<string, string>
        {
            ["query"] = name,
            ["include_adult"] = cfg.EnableAdultMetadata ? "true" : "false"
        };
        if (year.HasValue) query["first_air_date_year"] = year.Value.ToString(CultureInfo.InvariantCulture);
        return GetJsonAsync<TmdbSearchResponse>("/search/tv", query, cfg, ct);
    }

    public Task<TmdbSearchResponse?> SearchPersonAsync(string name, PluginConfiguration cfg, CancellationToken ct)
    {
        var query = new Dictionary<string, string>
        {
            ["query"] = name,
            ["include_adult"] = cfg.EnableAdultMetadata ? "true" : "false"
        };
        return GetJsonAsync<TmdbSearchResponse>("/search/person", query, cfg, ct);
    }

    public Task<TmdbTitle?> GetMovieAsync(string id, PluginConfiguration cfg, CancellationToken ct, bool omitConfiguredLanguage = false)
        => GetJsonAsync<TmdbTitle>($"/movie/{id}", new Dictionary<string, string> { ["append_to_response"] = "credits,images" }, cfg, ct, omitConfiguredLanguage: omitConfiguredLanguage);

    public Task<TmdbTitle?> GetSeriesAsync(string id, PluginConfiguration cfg, CancellationToken ct, bool omitConfiguredLanguage = false)
        => GetJsonAsync<TmdbTitle>($"/tv/{id}", new Dictionary<string, string> { ["append_to_response"] = "credits,images" }, cfg, ct, omitConfiguredLanguage: omitConfiguredLanguage);

    public Task<TmdbTitle?> GetSeasonAsync(string seriesId, int seasonNumber, PluginConfiguration cfg, CancellationToken ct, bool omitConfiguredLanguage = false)
        => GetJsonAsync<TmdbTitle>($"/tv/{seriesId}/season/{seasonNumber}", new Dictionary<string, string>(), cfg, ct, omitConfiguredLanguage: omitConfiguredLanguage);

    public Task<TmdbTitle?> GetEpisodeAsync(string seriesId, int season, int episode, PluginConfiguration cfg, CancellationToken ct, bool omitConfiguredLanguage = false)
        => GetJsonAsync<TmdbTitle>($"/tv/{seriesId}/season/{season}/episode/{episode}", new Dictionary<string, string>(), cfg, ct, omitConfiguredLanguage: omitConfiguredLanguage);

    public Task<TmdbPerson?> GetPersonAsync(string id, PluginConfiguration cfg, CancellationToken ct, bool omitConfiguredLanguage = false)
        => GetJsonAsync<TmdbPerson>($"/person/{id}", new Dictionary<string, string>(), cfg, ct, omitConfiguredLanguage: omitConfiguredLanguage);

    public string GetImageUrl(string? path, string size = "original")
        => string.IsNullOrWhiteSpace(path) ? string.Empty : $"https://image.tmdb.org/t/p/{size}{path}";

    private static string BuildBaseUrl(PluginConfiguration cfg)
    {
        var configured = TmdbUrlHelper.ResolveApiBaseUrl(cfg.TmdbApiBaseUrl);
        return string.IsNullOrWhiteSpace(cfg.TmdbApiBaseUrl) ? TmdbUrlHelper.SystemDefaultApiBaseUrl + "/3" : configured;
    }

    private async Task<T?> GetJsonAsync<T>(string path, IDictionary<string, string> query, PluginConfiguration cfg, CancellationToken ct, bool omitConfiguredLanguage = false)
    {
        if (string.IsNullOrWhiteSpace(cfg.TmdbApiKey))
        {
            _log.Warn("TMDB API key is empty. EmbyTMDBScraperFix provider will not return remote metadata.");
            return default;
        }

        query["api_key"] = cfg.TmdbApiKey;
        if (!omitConfiguredLanguage)
        {
            query["language"] = string.IsNullOrWhiteSpace(cfg.TmdbLanguage) ? "zh-CN" : cfg.TmdbLanguage;
        }
        if (!string.IsNullOrWhiteSpace(cfg.TmdbRegion)) query["region"] = cfg.TmdbRegion;

        var url = BuildBaseUrl(cfg) + path + "?" + BuildQuery(query);

        try
        {
            using var stream = await _http.GetStreamAsync(new Uri(url), cfg, ct).ConfigureAwait(false);
            return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log.Error($"TMDB request failed: {TmdbUrlHelper.SanitizeUrl(url)}. ProxyEnabled={cfg.ProxyEnabled}, ProxyHost={cfg.ProxyHost}, ProxyPort={cfg.ProxyPort}", ex);
            throw;
        }
    }

    private static string BuildQuery(IDictionary<string, string> query)
    {
        var parts = new List<string>();
        foreach (var kv in query)
        {
            parts.Add(Uri.EscapeDataString(kv.Key) + "=" + Uri.EscapeDataString(kv.Value ?? string.Empty));
        }
        return string.Join("&", parts);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
}

public sealed class TmdbSearchResponse
{
    [JsonPropertyName("results")]
    public List<TmdbSearchItem> Results { get; set; } = new();
}

public sealed class TmdbSearchItem
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("original_title")]
    public string? Original_Title { get; set; }

    [JsonPropertyName("original_name")]
    public string? Original_Name { get; set; }

    [JsonPropertyName("overview")]
    public string? Overview { get; set; }

    [JsonPropertyName("release_date")]
    public string? Release_Date { get; set; }

    [JsonPropertyName("first_air_date")]
    public string? First_Air_Date { get; set; }

    [JsonPropertyName("poster_path")]
    public string? Poster_Path { get; set; }

    [JsonPropertyName("backdrop_path")]
    public string? Backdrop_Path { get; set; }

    [JsonPropertyName("profile_path")]
    public string? Profile_Path { get; set; }

    [JsonPropertyName("vote_average")]
    public double Vote_Average { get; set; }
}

public sealed class TmdbTitle
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("original_title")]
    public string? Original_Title { get; set; }

    [JsonPropertyName("original_name")]
    public string? Original_Name { get; set; }

    [JsonPropertyName("overview")]
    public string? Overview { get; set; }

    [JsonPropertyName("release_date")]
    public string? Release_Date { get; set; }

    [JsonPropertyName("first_air_date")]
    public string? First_Air_Date { get; set; }

    [JsonPropertyName("air_date")]
    public string? Air_Date { get; set; }

    [JsonPropertyName("poster_path")]
    public string? Poster_Path { get; set; }

    [JsonPropertyName("backdrop_path")]
    public string? Backdrop_Path { get; set; }

    [JsonPropertyName("vote_average")]
    public double Vote_Average { get; set; }

    [JsonPropertyName("runtime")]
    public int? Runtime { get; set; }

    [JsonPropertyName("genres")]
    public List<TmdbGenre> Genres { get; set; } = new();

    [JsonPropertyName("credits")]
    public TmdbCredits? Credits { get; set; }

    [JsonPropertyName("images")]
    public TmdbImages? Images { get; set; }
}

public sealed class TmdbPerson
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("biography")]
    public string? Biography { get; set; }

    [JsonPropertyName("birthday")]
    public string? Birthday { get; set; }

    [JsonPropertyName("profile_path")]
    public string? Profile_Path { get; set; }
}

public sealed class TmdbGenre
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

public sealed class TmdbCredits
{
    [JsonPropertyName("cast")]
    public List<TmdbCast> Cast { get; set; } = new();

    [JsonPropertyName("crew")]
    public List<TmdbCrew> Crew { get; set; } = new();
}

public sealed class TmdbCast
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("character")]
    public string? Character { get; set; }

    [JsonPropertyName("order")]
    public int Order { get; set; }

    [JsonPropertyName("profile_path")]
    public string? Profile_Path { get; set; }
}

public sealed class TmdbCrew
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("job")]
    public string? Job { get; set; }

    [JsonPropertyName("profile_path")]
    public string? Profile_Path { get; set; }
}

public sealed class TmdbImages
{
    [JsonPropertyName("posters")]
    public List<TmdbImage> Posters { get; set; } = new();

    [JsonPropertyName("backdrops")]
    public List<TmdbImage> Backdrops { get; set; } = new();
}

public sealed class TmdbImage
{
    [JsonPropertyName("file_path")]
    public string? File_Path { get; set; }
}
