using System;
using System.Linq;

namespace EmbyTMDBScraperFix.Services;

internal static class TmdbUrlHelper
{
    public const string SystemDefaultApiBaseUrl = "https://api.themoviedb.org";
    public const string AlternativeApiBaseUrl = "https://api.tmdb.org";

    public static string ResolveApiBaseUrl(string? configuredBaseUrl)
    {
        var raw = string.IsNullOrWhiteSpace(configuredBaseUrl)
            ? SystemDefaultApiBaseUrl
            : configuredBaseUrl.Trim().TrimEnd('/');

        if (raw.IndexOf("://", StringComparison.Ordinal) < 0)
        {
            raw = "https://" + raw;
        }

        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri))
        {
            return SystemDefaultApiBaseUrl + "/3";
        }

        if (!uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return SystemDefaultApiBaseUrl + "/3";
        }

        var path = uri.AbsolutePath.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(path) || path == "/")
        {
            path = "/3";
        }
        else if (!path.EndsWith("/3", StringComparison.OrdinalIgnoreCase))
        {
            path += "/3";
        }

        var builder = new UriBuilder(uri)
        {
            Path = path,
            Query = string.Empty,
            Fragment = string.Empty
        };

        return builder.Uri.ToString().TrimEnd('/');
    }

    public static bool IsTmdbApiHost(string host, string? configuredBaseUrl = null)
    {
        if (host.Equals("api.themoviedb.org", StringComparison.OrdinalIgnoreCase)
            || host.Equals("api.tmdb.org", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var configuredHost = GetApiHost(configuredBaseUrl);
        return !string.IsNullOrWhiteSpace(configuredHost)
            && host.Equals(configuredHost, StringComparison.OrdinalIgnoreCase);
    }

    public static string? GetApiHost(string? configuredBaseUrl)
    {
        var resolved = ResolveApiBaseUrl(configuredBaseUrl);
        return Uri.TryCreate(resolved, UriKind.Absolute, out var uri) ? uri.Host : null;
    }

    public static string SanitizeUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return url ?? string.Empty;
        }

        var builder = new UriBuilder(uri);
        if (!string.IsNullOrWhiteSpace(builder.Query))
        {
            var pairs = builder.Query.TrimStart('?')
                .Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(static part =>
                {
                    var idx = part.IndexOf('=');
                    var key = idx >= 0 ? part.Substring(0, idx) : part;
                    return key.Equals("api_key", StringComparison.OrdinalIgnoreCase) ? key + "=***" : part;
                });
            builder.Query = string.Join("&", pairs);
        }

        return builder.Uri.ToString();
    }
}
