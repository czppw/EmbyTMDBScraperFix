using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using EmbyTMDBScraperFix.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.IO;
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

[Route("/EmbyTMDBScraperFix/Diagnostics/ListIndexedItems", "GET", Summary = "List indexed Emby items under a path prefix")]
public sealed class ListFixIndexedItems : IReturn<object>
{
    public string Path { get; set; } = string.Empty;
    public int Limit { get; set; } = 200;
}

[Route("/EmbyTMDBScraperFix/Diagnostics/ResolveInternalId", "GET", Summary = "Resolve an Emby internal item id")]
public sealed class ResolveFixInternalId : IReturn<object>
{
    public long Id { get; set; }
}

[Route("/EmbyTMDBScraperFix/Diagnostics/RefreshItem", "POST", Summary = "Refresh a specific Emby item by internal id")]
public sealed class RefreshFixItem : IReturn<object>
{
    public long Id { get; set; }
    public bool Recursive { get; set; }
    public bool ReplaceAllMetadata { get; set; }
}

[Route("/EmbyTMDBScraperFix/Diagnostics/FillEpisodeNumbersFromPath", "POST", Summary = "Re-parse a specific episode's season and episode numbers from its path")]
public sealed class FillFixEpisodeNumbersFromPath : IReturn<object>
{
    public long Id { get; set; }
    public bool ForceRefresh { get; set; } = true;
    public bool Persist { get; set; }
    public bool RefreshMetadataAfterPersist { get; set; }
}

public sealed class ItemDiagnosticInfo
{
    public string EntityId { get; set; } = string.Empty;
    public long? InternalId { get; set; }
    public string RuntimeType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string ContainingFolderPath { get; set; } = string.Empty;
    public string LocationType { get; set; } = string.Empty;
    public bool? IsLocked { get; set; }
    public string[] LockedFields { get; set; } = Array.Empty<string>();
    public string DateCreated { get; set; } = string.Empty;
    public string DateModified { get; set; } = string.Empty;
    public int? IndexNumber { get; set; }
    public int? ParentIndexNumber { get; set; }
    public int? RecursiveItemCount { get; set; }
    public Dictionary<string, string> ProviderIds { get; set; } = new();
    public string? SeasonSeriesName { get; set; }
    public string? SeasonSeriesPath { get; set; }
    public string? EpisodeSeriesName { get; set; }
}

public sealed class ConfigurationService : IService
{
    private readonly ILibraryManager _libraryManager;
    private readonly IProviderManager _providerManager;
    private readonly IFileSystem _fileSystem;

    public ConfigurationService(ILibraryManager libraryManager, IProviderManager providerManager, IFileSystem fileSystem)
    {
        _libraryManager = libraryManager;
        _providerManager = providerManager;
        _fileSystem = fileSystem;
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

    public object Get(ListFixIndexedItems request)
    {
        var path = request.Path?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path is required.");
        }

        var query = new InternalItemsQuery
        {
            PathStartsWith = path,
            PathIgnoreCase = true,
            Recursive = true
        };

        var items = _libraryManager.GetItemList(query)
            .Take(Math.Max(1, request.Limit))
            .Select(DescribeItem)
            .Where(x => x != null)
            .ToList();

        return new
        {
            inputPath = path,
            count = items.Count,
            items
        };
    }

    public object Get(ResolveFixInternalId request)
    {
        if (request.Id <= 0)
        {
            throw new ArgumentException("Id must be greater than 0.");
        }

        var query = new InternalItemsQuery
        {
            ItemIds = new[] { request.Id }
        };

        var item = _libraryManager.GetItemList(query).FirstOrDefault();
        return new
        {
            inputId = request.Id,
            item = DescribeItem(item)
        };
    }

    public async Task<object> Post(RefreshFixItem request)
    {
        var item = GetRequiredItem(request.Id);
        var before = DescribeItem(item);

        var options = CreateRefreshOptions(request.Recursive, request.ReplaceAllMetadata);
        await _providerManager.RefreshFullItem(item, options, CancellationToken.None).ConfigureAwait(false);

        return new
        {
            inputId = request.Id,
            before,
            after = DescribeItem(GetRequiredItem(request.Id))
        };
    }

    public async Task<object> Post(FillFixEpisodeNumbersFromPath request)
    {
        if (request.Id <= 0)
        {
            throw new ArgumentException("Id must be greater than 0.");
        }

        if (GetRequiredItem(request.Id) is not Episode episode)
        {
            throw new ArgumentException($"Item {request.Id} is not an episode.");
        }

        var before = DescribeItem(episode);
        var updated = _libraryManager.FillMissingEpisodeNumbersFromPath(episode, request.ForceRefresh);
        var afterParse = DescribeItem(episode);

        ItemDiagnosticInfo? afterPersist = null;
        if (updated && request.Persist)
        {
            await _providerManager.SaveMetadata(episode, ItemUpdateType.MetadataEdit).ConfigureAwait(false);

            if (request.RefreshMetadataAfterPersist)
            {
                await _providerManager.RefreshFullItem(
                    episode,
                    CreateRefreshOptions(false, false),
                    CancellationToken.None).ConfigureAwait(false);
            }

            afterPersist = DescribeItem(GetRequiredItem(request.Id));
        }

        return new
        {
            inputId = request.Id,
            forceRefresh = request.ForceRefresh,
            updated,
            persisted = request.Persist,
            refreshedMetadata = request.Persist && request.RefreshMetadataAfterPersist,
            before,
            afterParse,
            afterPersist
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

    private static ItemDiagnosticInfo? DescribeItem(BaseItem? item)
    {
        if (item == null)
        {
            return null;
        }

        return new ItemDiagnosticInfo
        {
            EntityId = item.Id.ToString(),
            InternalId = ReadInt64Property(item, "InternalId"),
            RuntimeType = item.GetType().FullName ?? string.Empty,
            Name = item.Name ?? string.Empty,
            Path = item.Path ?? string.Empty,
            ContainingFolderPath = item.ContainingFolderPath ?? string.Empty,
            LocationType = item.LocationType.ToString(),
            IsLocked = ReadBooleanProperty(item, "IsLocked"),
            LockedFields = item.LockedFields.Select(x => x.ToString()).ToArray(),
            DateCreated = item.DateCreated.ToString("O"),
            DateModified = item.DateModified.ToString("O"),
            IndexNumber = item.IndexNumber,
            ParentIndexNumber = item.ParentIndexNumber,
            RecursiveItemCount = item.RecursiveItemCount,
            ProviderIds = item.ProviderIds.ToDictionary(x => x.Key, x => x.Value),
            SeasonSeriesName = item is Season season ? season.SeriesName : null,
            SeasonSeriesPath = item is Season seasonWithSeries ? seasonWithSeries.Series?.Path : null,
            EpisodeSeriesName = item is Episode episode ? episode.SeriesName : null
        };
    }

    private BaseItem GetRequiredItem(long id)
    {
        var item = _libraryManager.GetItemList(new InternalItemsQuery
        {
            ItemIds = new[] { id }
        }).FirstOrDefault();

        return item ?? throw new ArgumentException($"Item {id} was not found.");
    }

    private MetadataRefreshOptions CreateRefreshOptions(bool recursive, bool replaceAllMetadata)
    {
        return new MetadataRefreshOptions(_fileSystem)
        {
            MetadataRefreshMode = MetadataRefreshMode.FullRefresh,
            ImageRefreshMode = MetadataRefreshMode.FullRefresh,
            ReplaceAllMetadata = replaceAllMetadata,
            EnableRemoteContentProbe = true,
            EnableSubtitleDownloading = true,
            IsAutomated = false,
            Recursive = recursive
        };
    }

    private static bool? ReadBooleanProperty(object instance, string propertyName)
    {
        var property = instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        if (property?.PropertyType != typeof(bool) || !property.CanRead)
        {
            return null;
        }

        return (bool)property.GetValue(instance)!;
    }

    private static long? ReadInt64Property(object instance, string propertyName)
    {
        var property = instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
        if (property == null || !property.CanRead)
        {
            return null;
        }

        var value = property.GetValue(instance);
        if (value is long longValue)
        {
            return longValue;
        }

        if (value is int intValue)
        {
            return intValue;
        }

        return null;
    }
}
