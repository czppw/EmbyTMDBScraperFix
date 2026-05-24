using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EmbyTMDBScraperFix.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
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
    public string TmdbApiBaseUrl { get; set; } = "https://api.tmdb.org";
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
    public bool ProxyEnabled { get; set; }
    public string ProxyHost { get; set; } = string.Empty;
    public int ProxyPort { get; set; }
    public string ProxyUsername { get; set; } = string.Empty;
    public string ProxyPassword { get; set; } = string.Empty;
    public bool EnableLegacyGlobalProxyHook { get; set; }
    public string TmdbApiKey { get; set; } = string.Empty;
    public string TmdbApiBaseUrl { get; set; } = "https://api.tmdb.org";
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

[Route("/EmbyTMDBScraperFix/Diagnostics/ResolvePath", "GET", Summary = "Resolve how Emby maps a path to items")]
public sealed class ResolveFixPath : IReturn<object>
{
    public string Path { get; set; } = string.Empty;
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

    public object Get(ResolveFixPath request)
    {
        var path = request.Path?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path is required.");
        }

        var normalized = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var parent = Path.GetDirectoryName(normalized);
        var grandParent = string.IsNullOrWhiteSpace(parent) ? null : Path.GetDirectoryName(parent);

        return new
        {
            inputPath = path,
            existsAsFile = File.Exists(path),
            existsAsDirectory = Directory.Exists(path),
            directAuto = DescribeItem(_libraryManager.FindByPath(path, null)),
            directFile = DescribeItem(_libraryManager.FindByPath(path, false)),
            directFolder = DescribeItem(_libraryManager.FindByPath(path, true)),
            normalizedAuto = normalized.Equals(path, StringComparison.Ordinal) ? null : DescribeItem(_libraryManager.FindByPath(normalized, null)),
            parentAuto = !string.IsNullOrWhiteSpace(parent) ? DescribeItem(_libraryManager.FindByPath(parent, null)) : null,
            parentFolder = !string.IsNullOrWhiteSpace(parent) ? DescribeItem(_libraryManager.FindByPath(parent, true)) : null,
            grandParentAuto = !string.IsNullOrWhiteSpace(grandParent) ? DescribeItem(_libraryManager.FindByPath(grandParent, null)) : null,
            grandParentFolder = !string.IsNullOrWhiteSpace(grandParent) ? DescribeItem(_libraryManager.FindByPath(grandParent, true)) : null
        };
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
        cfg.TmdbApiBaseUrl = request.TmdbApiBaseUrl?.Trim() ?? string.Empty;
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
        var current = PluginRuntime.Instance.Configuration;
        var cfg = new PluginConfiguration
        {
            ProxyEnabled = request.ProxyEnabled,
            ProxyHost = request.ProxyHost ?? string.Empty,
            ProxyPort = request.ProxyPort,
            ProxyUsername = request.ProxyUsername ?? string.Empty,
            ProxyPassword = request.ProxyPassword ?? string.Empty,
            EnableLegacyGlobalProxyHook = request.EnableLegacyGlobalProxyHook,
            TmdbApiKey = request.TmdbApiKey ?? string.Empty,
            TmdbApiBaseUrl = request.TmdbApiBaseUrl?.Trim() ?? string.Empty,
            TmdbLanguage = current.TmdbLanguage,
            TmdbRegion = current.TmdbRegion,
            EnableAdultMetadata = current.EnableAdultMetadata,
            EnableTvdbFallback = current.EnableTvdbFallback,
            TvdbApiKey = current.TvdbApiKey,
            TvdbPin = current.TvdbPin,
            TvdbLanguage = current.TvdbLanguage,
            AutoScanEnabled = current.AutoScanEnabled,
            ScanIntervalMinutes = current.ScanIntervalMinutes,
            AutoMetadataRefresh = current.AutoMetadataRefresh,
            MaxScrapeRetryCount = current.MaxScrapeRetryCount,
            Libraries = current.Libraries,
            MetadataProxyHosts = current.MetadataProxyHosts
        };

        return await PluginRuntime.Instance.ProxyClient.TestProxyAsync(cfg, default).ConfigureAwait(false);
    }

    private static object? DescribeItem(BaseItem? item)
    {
        if (item == null)
        {
            return null;
        }

        return new
        {
            runtimeType = item.GetType().FullName,
            name = item.Name,
            path = item.Path,
            containingFolderPath = item.ContainingFolderPath,
            indexNumber = item.IndexNumber,
            parentIndexNumber = item.ParentIndexNumber,
            recursiveItemCount = item.RecursiveItemCount,
            providerIds = item.ProviderIds.ToDictionary(x => x.Key, x => x.Value),
            seasonSeriesName = item is Season season ? season.SeriesName : null,
            seasonSeriesPath = item is Season seasonWithSeries ? seasonWithSeries.Series?.Path : null,
            episodeSeriesName = item is Episode episode ? episode.SeriesName : null
        };
    }
}
