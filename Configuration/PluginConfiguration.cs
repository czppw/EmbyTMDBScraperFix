using System.Collections.Generic;
using MediaBrowser.Model.Plugins;

namespace EmbyTMDBScraperFix.Configuration;

public class PluginConfiguration : BasePluginConfiguration
{
    public bool ProxyEnabled { get; set; } = false;
    public string ProxyHost { get; set; } = string.Empty;
    public int ProxyPort { get; set; } = 7890;
    public string ProxyUsername { get; set; } = string.Empty;
    public string ProxyPassword { get; set; } = string.Empty;
    public bool EnableLegacyGlobalProxyHook { get; set; } = false;

    public string TmdbApiKey { get; set; } = string.Empty;
    public string TmdbApiBaseUrl { get; set; } = "https://api.tmdb.org";
    public string TmdbLanguage { get; set; } = "zh-CN";
    public string TmdbRegion { get; set; } = "CN";
    public bool EnableAdultMetadata { get; set; } = false;

    public bool EnableTvdbFallback { get; set; } = false;
    public string TvdbApiKey { get; set; } = string.Empty;
    public string TvdbPin { get; set; } = string.Empty;
    public string TvdbLanguage { get; set; } = "zho";

    public bool AutoScanEnabled { get; set; } = true;
    public int ScanIntervalMinutes { get; set; } = 10;
    public bool AutoMetadataRefresh { get; set; } = true;
    public int MaxScrapeRetryCount { get; set; } = 3;
    public List<LibraryScanOption> Libraries { get; set; } = new();

    public List<string> MetadataProxyHosts { get; set; } = new()
    {
        "api.themoviedb.org",
        "api.tmdb.org",
        "image.tmdb.org",
        "www.themoviedb.org",
        "api4.thetvdb.com",
        "artworks.thetvdb.com",
        "www.thetvdb.com",
        "thetvdb.com"
    };
}

public class LibraryScanOption
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
}
