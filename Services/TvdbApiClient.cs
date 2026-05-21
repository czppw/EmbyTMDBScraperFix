using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using EmbyTMDBScraperFix.Configuration;

namespace EmbyTMDBScraperFix.Services;

public sealed class TvdbApiClient
{
    private const string BaseUrl = "https://api4.thetvdb.com/v4";
    private readonly ProxyHttpClientService _http;
    private readonly PluginLogService _log;
    private string? _cachedToken;
    private string? _cachedApiKey;
    private string? _cachedPin;

    public TvdbApiClient(ProxyHttpClientService http, PluginLogService log)
    {
        _http = http;
        _log = log;
    }

    public async Task<TvdbSearchResponse?> SearchSeriesAsync(string name, PluginConfiguration cfg, CancellationToken ct)
    {
        var token = await EnsureTokenAsync(cfg, ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(token)) return null;
        var ensuredToken = token!;
        return await GetJsonAsync<TvdbSearchResponse>($"/search?query={Uri.EscapeDataString(name)}&type=series", cfg, ensuredToken, ct).ConfigureAwait(false);
    }

    public async Task<TvdbSearchResponse?> SearchPersonAsync(string name, PluginConfiguration cfg, CancellationToken ct)
    {
        var token = await EnsureTokenAsync(cfg, ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(token)) return null;
        var ensuredToken = token!;
        return await GetJsonAsync<TvdbSearchResponse>($"/search?query={Uri.EscapeDataString(name)}&type=people", cfg, ensuredToken, ct).ConfigureAwait(false);
    }

    public async Task<TvdbSeriesResponse?> GetSeriesAsync(string id, PluginConfiguration cfg, CancellationToken ct)
    {
        var token = await EnsureTokenAsync(cfg, ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(token)) return null;
        var ensuredToken = token!;
        return await GetJsonAsync<TvdbSeriesResponse>($"/series/{id}/extended", cfg, ensuredToken, ct).ConfigureAwait(false);
    }

    public async Task<TvdbSeasonResponse?> GetSeasonAsync(string id, PluginConfiguration cfg, CancellationToken ct)
    {
        var token = await EnsureTokenAsync(cfg, ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(token)) return null;
        var ensuredToken = token!;
        return await GetJsonAsync<TvdbSeasonResponse>($"/seasons/{id}/extended", cfg, ensuredToken, ct).ConfigureAwait(false);
    }

    public async Task<TvdbEpisodeResponse?> GetEpisodeAsync(string id, PluginConfiguration cfg, CancellationToken ct)
    {
        var token = await EnsureTokenAsync(cfg, ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(token)) return null;
        var ensuredToken = token!;
        return await GetJsonAsync<TvdbEpisodeResponse>($"/episodes/{id}/extended", cfg, ensuredToken, ct).ConfigureAwait(false);
    }

    public async Task<TvdbPersonResponse?> GetPersonAsync(string id, PluginConfiguration cfg, CancellationToken ct)
    {
        var token = await EnsureTokenAsync(cfg, ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(token)) return null;
        var ensuredToken = token!;
        return await GetJsonAsync<TvdbPersonResponse>($"/people/{id}/extended", cfg, ensuredToken, ct).ConfigureAwait(false);
    }

    public string NormalizeImageUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return string.Empty;
        return url.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? url : "https://artworks.thetvdb.com" + url;
    }

    private async Task<string?> EnsureTokenAsync(PluginConfiguration cfg, CancellationToken ct)
    {
        if (!cfg.EnableTvdbFallback || string.IsNullOrWhiteSpace(cfg.TvdbApiKey))
        {
            return null;
        }

        if (_cachedToken != null &&
            string.Equals(_cachedApiKey, cfg.TvdbApiKey, StringComparison.Ordinal) &&
            string.Equals(_cachedPin, cfg.TvdbPin, StringComparison.Ordinal))
        {
            return _cachedToken;
        }

        try
        {
            var body = new TvdbLoginRequest { ApiKey = cfg.TvdbApiKey, Pin = cfg.TvdbPin };
            var json = JsonSerializer.Serialize(body, JsonOptions);
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
            var uri = new Uri(BaseUrl + "/login");
            using var client = CreateHttpClient(uri, cfg, TimeSpan.FromSeconds(20));
            using var request = new HttpRequestMessage(HttpMethod.Post, uri)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            using var response = await client.SendAsync(request, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var payload = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var login = JsonSerializer.Deserialize<TvdbLoginResponse>(payload, JsonOptions);
            _cachedToken = login?.Data?.Token;
            _cachedApiKey = cfg.TvdbApiKey;
            _cachedPin = cfg.TvdbPin;
            return _cachedToken;
        }
        catch (Exception ex)
        {
            _log.Warn("TVDB login failed: " + ex.Message);
            return null;
        }
    }

    private async Task<T?> GetJsonAsync<T>(string relativeUrl, PluginConfiguration cfg, string token, CancellationToken ct)
    {
        var uri = new Uri(BaseUrl + relativeUrl);
        using var client = CreateHttpClient(uri, cfg, TimeSpan.FromSeconds(30));
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + token);
        using var response = await client.SendAsync(request, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            _log.Warn($"TVDB request failed: {relativeUrl} => {(int)response.StatusCode}");
            return default;
        }
        using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        if (stream == null)
        {
            return default;
        }

        var responseStream = stream;
        return await JsonSerializer.DeserializeAsync<T>(responseStream, JsonOptions, ct).ConfigureAwait(false);
    }

    private HttpClient CreateHttpClient(Uri uri, PluginConfiguration cfg, TimeSpan timeout)
    {
        var handler = new HttpClientHandler();
        var policy = new ProxyPolicyService(_log);
        if (policy.ShouldProxy(uri, cfg))
        {
            handler.UseProxy = true;
            handler.Proxy = policy.CreateProxy(cfg);
        }
        else
        {
            handler.UseProxy = false;
        }
        return new HttpClient(handler, disposeHandler: true) { Timeout = timeout };
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
}

public sealed class TvdbLoginRequest
{
    [JsonPropertyName("apikey")]
    public string ApiKey { get; set; } = string.Empty;

    [JsonPropertyName("pin")]
    public string Pin { get; set; } = string.Empty;
}

public sealed class TvdbLoginResponse
{
    [JsonPropertyName("data")]
    public TvdbLoginData? Data { get; set; }
}

public sealed class TvdbLoginData
{
    [JsonPropertyName("token")]
    public string? Token { get; set; }
}

public sealed class TvdbSearchResponse
{
    [JsonPropertyName("data")]
    public List<TvdbSearchItem> Data { get; set; } = new();
}

public sealed class TvdbSearchItem
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("tvdb_id")]
    public string? TvdbId { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("overview")]
    public string? Overview { get; set; }

    [JsonPropertyName("image_url")]
    public string? ImageUrl { get; set; }

    [JsonPropertyName("year")]
    public string? Year { get; set; }
}

public sealed class TvdbSeriesResponse
{
    [JsonPropertyName("data")]
    public TvdbSeriesData? Data { get; set; }
}

public sealed class TvdbSeriesData
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("overview")]
    public string? Overview { get; set; }

    [JsonPropertyName("image")]
    public string? Image { get; set; }

    [JsonPropertyName("firstAired")]
    public string? FirstAired { get; set; }

    [JsonPropertyName("year")]
    public string? Year { get; set; }

    [JsonPropertyName("seasons")]
    public List<TvdbSeasonSummary> Seasons { get; set; } = new();
}

public sealed class TvdbSeasonSummary
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("number")]
    public int? Number { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("image")]
    public string? Image { get; set; }

    [JsonPropertyName("overview")]
    public string? Overview { get; set; }
}

public sealed class TvdbSeasonResponse
{
    [JsonPropertyName("data")]
    public TvdbSeasonData? Data { get; set; }
}

public sealed class TvdbSeasonData
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("number")]
    public int? Number { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("image")]
    public string? Image { get; set; }

    [JsonPropertyName("overview")]
    public string? Overview { get; set; }

    [JsonPropertyName("episodes")]
    public List<TvdbEpisodeSummary> Episodes { get; set; } = new();
}

public sealed class TvdbEpisodeSummary
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("number")]
    public int? Number { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

public sealed class TvdbEpisodeResponse
{
    [JsonPropertyName("data")]
    public TvdbEpisodeData? Data { get; set; }
}

public sealed class TvdbEpisodeData
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("overview")]
    public string? Overview { get; set; }

    [JsonPropertyName("image")]
    public string? Image { get; set; }

    [JsonPropertyName("aired")]
    public string? Aired { get; set; }
}

public sealed class TvdbPersonResponse
{
    [JsonPropertyName("data")]
    public TvdbPersonData? Data { get; set; }
}

public sealed class TvdbPersonData
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("overview")]
    public string? Overview { get; set; }

    [JsonPropertyName("image")]
    public string? Image { get; set; }

    [JsonPropertyName("birth")]
    public string? Birth { get; set; }
}
