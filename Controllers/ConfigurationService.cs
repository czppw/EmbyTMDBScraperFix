using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EmbyTMDBScraperFix.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Services;

namespace EmbyTMDBScraperFix.Controllers;

[Route("/EmbyTMDBScraperFix/Configuration", "GET", Summary = "Get EmbyTMDBScraperFix configuration")]
public sealed class GetFixConfiguration : IReturn<PluginConfiguration>
{
}

[Route("/EmbyTMDBScraperFix/Configuration", "POST", Summary = "Update EmbyTMDBScraperFix configuration")]
public sealed class UpdateFixConfiguration : IReturn<PluginConfiguration>
{
    public bool ProxyEnabled { get; set; }
    public string ProxyHost { get; set; } = string.Empty;
    public int ProxyPort { get; set; }
    public string ProxyUsername { get; set; } = string.Empty;
    public string ProxyPassword { get; set; } = string.Empty;
    public bool EnableLegacyGlobalProxyHook { get; set; }

    public string TmdbApiKey { get; set; } = string.Empty;
    public string TmdbLanguage { get; set; } = "zh-CN";
    public string TmdbRegion { get; set; } = "CN";
    public bool EnableAdultMetadata { get; set; }

    public bool EnableTvdbFallback { get; set; }
    public string TvdbApiKey { get; set; } = string.Empty;
    public string TvdbPin { get; set; } = string.Empty;
    public string TvdbLanguage { get; set; } = "zho";

    public bool AutoScanEnabled { get; set; }
    public int ScanIntervalMinutes { get; set; }
    public bool AutoMetadataRefresh { get; set; } = true;
    public int MaxScrapeRetryCount { get; set; }
    public List<LibraryScanOption> Libraries { get; set; } = new();
}

[Route("/EmbyTMDBScraperFix/TestProxy", "POST", Summary = "Test proxy connectivity")]
public sealed class TestFixProxy : IReturn<object>
{
}

[Route("/EmbyTMDBScraperFix/Logs", "GET", Summary = "Get recent plugin logs")]
public sealed class GetFixLogs : IReturn<object>
{
    public int Limit { get; set; } = 200;
}

[Route("/EmbyTMDBScraperFix/Libraries", "GET", Summary = "Get Emby virtual libraries")]
public sealed class GetFixLibraries : IReturn<List<LibraryScanOption>>
{
}

public sealed class ConfigurationService : IService
{
    private readonly ILibraryManager _libraryManager;

    public ConfigurationService(ILibraryManager libraryManager)
    {
        _libraryManager = libraryManager;
    }

    public object Get(GetFixConfiguration request) => Plugin.Instance?.Configuration ?? new PluginConfiguration();

    public object Get(GetFixLogs request) => PluginRuntime.Instance.Log.GetRecent(Math.Max(1, request.Limit));

    public object Get(GetFixLibraries request)
    {
        var cfg = Plugin.Instance?.Configuration ?? new PluginConfiguration();
        var saved = cfg.Libraries ?? new List<LibraryScanOption>();

        return _libraryManager.GetVirtualFolders()
            .SelectMany(folder => (folder.Locations ?? Array.Empty<string>()).DefaultIfEmpty(string.Empty), (folder, path) =>
            {
                var id = !string.IsNullOrWhiteSpace(folder.Id) ? folder.Id : folder.Name;
                var match = saved.FirstOrDefault(x =>
                    string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(x.Path, path, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(x.Name, folder.Name, StringComparison.OrdinalIgnoreCase));

                return new LibraryScanOption
                {
                    Id = id ?? string.Empty,
                    Name = folder.Name ?? string.Empty,
                    Path = path ?? string.Empty,
                    Enabled = match?.Enabled ?? true
                };
            })
            .ToList();
    }

    public object Post(UpdateFixConfiguration request)
    {
        var plugin = Plugin.Instance ?? throw new InvalidOperationException("Plugin not initialized.");
        var cfg = plugin.Configuration;
        cfg.ProxyEnabled = request.ProxyEnabled;
        cfg.ProxyHost = request.ProxyHost ?? string.Empty;
        cfg.ProxyPort = request.ProxyPort;
        cfg.ProxyUsername = request.ProxyUsername ?? string.Empty;
        cfg.ProxyPassword = request.ProxyPassword ?? string.Empty;
        cfg.EnableLegacyGlobalProxyHook = request.EnableLegacyGlobalProxyHook;

        cfg.TmdbApiKey = request.TmdbApiKey ?? string.Empty;
        cfg.TmdbLanguage = string.IsNullOrWhiteSpace(request.TmdbLanguage) ? "zh-CN" : request.TmdbLanguage;
        cfg.TmdbRegion = string.IsNullOrWhiteSpace(request.TmdbRegion) ? "CN" : request.TmdbRegion;
        cfg.EnableAdultMetadata = request.EnableAdultMetadata;

        cfg.EnableTvdbFallback = request.EnableTvdbFallback;
        cfg.TvdbApiKey = request.TvdbApiKey ?? string.Empty;
        cfg.TvdbPin = request.TvdbPin ?? string.Empty;
        cfg.TvdbLanguage = string.IsNullOrWhiteSpace(request.TvdbLanguage) ? "zho" : request.TvdbLanguage;

        cfg.AutoScanEnabled = request.AutoScanEnabled;
        cfg.ScanIntervalMinutes = Math.Max(1, request.ScanIntervalMinutes);
        cfg.AutoMetadataRefresh = request.AutoMetadataRefresh;
        cfg.MaxScrapeRetryCount = Math.Max(0, request.MaxScrapeRetryCount);
        cfg.Libraries = request.Libraries ?? new List<LibraryScanOption>();
        plugin.SaveConfiguration();
        PluginRuntime.Instance.ApplyConfiguration();
        return cfg;
    }

    public async Task<object> Post(TestFixProxy request)
    {
        return await PluginRuntime.Instance.ProxyClient.TestProxyAsync(PluginRuntime.Instance.Configuration, default).ConfigureAwait(false);
    }
}
