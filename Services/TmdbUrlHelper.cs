using System;

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

    public static bool IsTmdbApiHost(string host)
    {
        return host.Equals("api.themoviedb.org", StringComparison.OrdinalIgnoreCase)
            || host.Equals("api.tmdb.org", StringComparison.OrdinalIgnoreCase);
    }
}
