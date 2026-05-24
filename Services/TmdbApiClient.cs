using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
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

    public async Task<TmdbSearchResponse?> SearchMovieAsync(string name, int? year, PluginConfiguration cfg, CancellationToken ct)
    {
        var query = new Dictionary<string, string>
        {
            ["query"] = name,
            ["include_adult"] = cfg.EnableAdultMetadata ? "true" : "false"
        };
        if (year.HasValue)
        {
            query["year"] = year.Value.ToString(CultureInfo.InvariantCulture);
        }

        return await SearchWithFallbackAsync("/search/movie", query, cfg, ct, hasYearConstraint: year.HasValue, yearKey: "year").ConfigureAwait(false);
    }

    public async Task<TmdbSearchResponse?> SearchSeriesAsync(string name, int? year, PluginConfiguration cfg, CancellationToken ct)
    {
        var query = new Dictionary<string, string>
        {
            ["query"] = name,
            ["include_adult"] = cfg.EnableAdultMetadata ? "true" : "false"
        };
        if (year.HasValue)
        {
            query["first_air_date_year"] = year.Value.ToString(CultureInfo.InvariantCulture);
        }

        return await SearchWithFallbackAsync("/search/tv", query, cfg, ct, hasYearConstraint: year.HasValue, yearKey: "first_air_date_year").ConfigureAwait(false);
    }

    public async Task<TmdbSearchResponse?> SearchPersonAsync(string name, PluginConfiguration cfg, CancellationToken ct)
    {
        var query = new Dictionary<string, string>
        {
            ["query"] = name,
            ["include_adult"] = cfg.EnableAdultMetadata ? "true" : "false"
        };
        return await SearchWithFallbackAsync("/search/person", query, cfg, ct, hasYearConstraint: false, yearKey: string.Empty).ConfigureAwait(false);
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

    private async Task<TmdbSearchResponse?> SearchWithFallbackAsync(string path, Dictionary<string, string> query, PluginConfiguration cfg, CancellationToken ct, bool hasYearConstraint, string yearKey)
    {
        var originalQuery = query.TryGetValue("query", out var rawQuery) ? rawQuery : string.Empty;
        foreach (var queryVariant in BuildSearchQueryVariants(originalQuery))
        {
            var variantQuery = new Dictionary<string, string>(query, StringComparer.Ordinal)
            {
                ["query"] = queryVariant
            };

            var response = await GetJsonAsync<TmdbSearchResponse>(path, variantQuery, cfg, ct).ConfigureAwait(false);
            if (HasSearchResults(response))
            {
                SortSearchResults(response!, originalQuery);
                return response;
            }

            if (hasYearConstraint && !string.IsNullOrWhiteSpace(yearKey))
            {
                var withoutYear = new Dictionary<string, string>(variantQuery, StringComparer.Ordinal);
                withoutYear.Remove(yearKey);
                response = await GetJsonAsync<TmdbSearchResponse>(path, withoutYear, cfg, ct).ConfigureAwait(false);
                if (HasSearchResults(response))
                {
                    SortSearchResults(response!, originalQuery);
                    return response;
                }
            }

            if (!string.IsNullOrWhiteSpace(cfg.TmdbLanguage))
            {
                response = await GetJsonAsync<TmdbSearchResponse>(path, variantQuery, cfg, ct, omitConfiguredLanguage: true).ConfigureAwait(false);
                if (HasSearchResults(response))
                {
                    SortSearchResults(response!, originalQuery);
                    return response;
                }

                if (hasYearConstraint && !string.IsNullOrWhiteSpace(yearKey))
                {
                    var withoutYear = new Dictionary<string, string>(variantQuery, StringComparer.Ordinal);
                    withoutYear.Remove(yearKey);
                    response = await GetJsonAsync<TmdbSearchResponse>(path, withoutYear, cfg, ct, omitConfiguredLanguage: true).ConfigureAwait(false);
                    if (HasSearchResults(response))
                    {
                        SortSearchResults(response!, originalQuery);
                        return response;
                    }
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(originalQuery))
        {
            _log.Warn($"TMDB search returned no results for query '{originalQuery}' on path '{path}'.");
        }

        return new TmdbSearchResponse();
    }

    private static bool HasSearchResults(TmdbSearchResponse? response)
        => response?.Results != null && response.Results.Count > 0;

    private static List<string> BuildSearchQueryVariants(string query)
    {
        var variants = new List<string>();
        AddVariant(variants, query);

        var withoutBracketed = StripBracketedSegments(query);
        AddVariant(variants, withoutBracketed);

        var normalized = NormalizePunctuation(withoutBracketed);
        AddVariant(variants, normalized);

        var spacedTrailingYear = InsertSpaceBeforeTrailingYear(normalized);
        AddVariant(variants, spacedTrailingYear);

        var withoutTrailingYear = RemoveTrailingYear(normalized);
        AddVariant(variants, withoutTrailingYear);

        return variants;
    }

    private static void AddVariant(List<string> variants, string? candidate)
    {
        var value = CollapseWhitespace(candidate);
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        if (!variants.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            variants.Add(value);
        }
    }

    private static string CollapseWhitespace(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        var previousWasWhitespace = false;
        foreach (var ch in value.Trim())
        {
            if (char.IsWhiteSpace(ch))
            {
                if (!previousWasWhitespace)
                {
                    builder.Append(' ');
                    previousWasWhitespace = true;
                }

                continue;
            }

            builder.Append(ch);
            previousWasWhitespace = false;
        }

        return builder.ToString();
    }

    private static string StripBracketedSegments(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        var depth = 0;
        foreach (var ch in value)
        {
            if (ch is '(' or '[' or '{' or '（' or '【')
            {
                depth++;
                continue;
            }

            if (ch is ')' or ']' or '}' or '）' or '】')
            {
                if (depth > 0)
                {
                    depth--;
                }

                continue;
            }

            if (depth == 0)
            {
                builder.Append(ch);
            }
        }

        return builder.ToString();
    }

    private static string NormalizePunctuation(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            builder.Append(ch switch
            {
                '.' or '_' or '-' or '·' or ':' or '：' or '/' or '\\' => ' ',
                _ => ch
            });
        }

        return builder.ToString();
    }

    private static string InsertSpaceBeforeTrailingYear(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        if (!EndsWithFourDigits(trimmed))
        {
            return trimmed;
        }

        var splitIndex = trimmed.Length - 4;
        if (splitIndex <= 0 || char.IsWhiteSpace(trimmed[splitIndex - 1]))
        {
            return trimmed;
        }

        return trimmed.Insert(splitIndex, " ");
    }

    private static string RemoveTrailingYear(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        if (!EndsWithFourDigits(trimmed))
        {
            return trimmed;
        }

        return trimmed.Substring(0, trimmed.Length - 4).TrimEnd();
    }

    private static bool EndsWithFourDigits(string value)
        => value.Length >= 4
           && char.IsDigit(value[value.Length - 1])
           && char.IsDigit(value[value.Length - 2])
           && char.IsDigit(value[value.Length - 3])
           && char.IsDigit(value[value.Length - 4]);

    private static void SortSearchResults(TmdbSearchResponse response, string originalQuery)
    {
        response.Results = response.Results
            .OrderByDescending(x => ScoreSearchItem(x, originalQuery))
            .ThenByDescending(x => x.Vote_Average)
            .ThenByDescending(x => ParseDateValue(x.First_Air_Date ?? x.Release_Date))
            .ToList();
    }

    private static int ScoreSearchItem(TmdbSearchItem item, string originalQuery)
    {
        var normalizedOriginal = NormalizeForComparison(originalQuery);
        var normalizedLocalized = NormalizeForComparison(item.Name ?? item.Title);
        var normalizedOriginalName = NormalizeForComparison(item.Original_Name ?? item.Original_Title);
        var score = 0;

        if (!string.IsNullOrWhiteSpace(normalizedOriginal))
        {
            score = Math.Max(score, ScoreName(normalizedLocalized, normalizedOriginal));
            score = Math.Max(score, ScoreName(normalizedOriginalName, normalizedOriginal));

            var trailingDigits = ExtractTrailingDigits(normalizedOriginal);
            if (!string.IsNullOrWhiteSpace(trailingDigits))
            {
                if (normalizedLocalized.EndsWith(trailingDigits, StringComparison.Ordinal) || normalizedOriginalName.EndsWith(trailingDigits, StringComparison.Ordinal))
                {
                    score += 150;
                }
            }
        }

        return score;
    }

    private static int ScoreName(string candidate, string original)
    {
        if (string.IsNullOrWhiteSpace(candidate) || string.IsNullOrWhiteSpace(original))
        {
            return 0;
        }

        if (candidate.Equals(original, StringComparison.Ordinal))
        {
            return 1000;
        }

        if (candidate.IndexOf(original, StringComparison.Ordinal) >= 0 || original.IndexOf(candidate, StringComparison.Ordinal) >= 0)
        {
            return 700;
        }

        var originalWithoutTrailingYear = NormalizeForComparison(RemoveTrailingYear(original));
        if (!string.IsNullOrWhiteSpace(originalWithoutTrailingYear)
            && (candidate.IndexOf(originalWithoutTrailingYear, StringComparison.Ordinal) >= 0 || originalWithoutTrailingYear.IndexOf(candidate, StringComparison.Ordinal) >= 0))
        {
            return 500;
        }

        return 0;
    }

    private static string NormalizeForComparison(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(char.ToLowerInvariant(ch));
            }
        }

        return builder.ToString();
    }

    private static string ExtractTrailingDigits(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var index = value.Length;
        while (index > 0 && char.IsDigit(value[index - 1]))
        {
            index--;
        }

        return index == value.Length ? string.Empty : value.Substring(index);
    }

    private static DateTime ParseDateValue(string? value)
    {
        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed
            : DateTime.MinValue;
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
