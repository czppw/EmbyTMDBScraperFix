using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EmbyTMDBScraperFix.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.IO;

namespace EmbyTMDBScraperFix.Services;

public sealed class IncrementalScanService : IDisposable
{
    private const int MaxTrackedEvents = 10000;
    private readonly ILibraryManager _libraryManager;
    private readonly IProviderManager _providerManager;
    private readonly IFileSystem _fileSystem;
    private readonly PluginLogService _log;
    private readonly string _stateFilePath;
    private readonly object _gate = new();
    private readonly Dictionary<string, ChangeEntry> _pendingChanges = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, FileSystemWatcher> _watchers = new(StringComparer.OrdinalIgnoreCase);
    private volatile bool _running;

    private static readonly HashSet<string> MediaExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mkv", ".mp4", ".avi", ".mov", ".wmv", ".m4v", ".ts", ".m2ts", ".flv", ".webm",
        ".mp3", ".flac", ".aac", ".m4a", ".wav", ".ape", ".ogg",
        ".nfo", ".srt", ".ass", ".ssa", ".sub"
    };

    public IncrementalScanService(IApplicationPaths paths, ILibraryManager libraryManager, IProviderManager providerManager, IFileSystem fileSystem, PluginLogService log)
    {
        _libraryManager = libraryManager;
        _providerManager = providerManager;
        _fileSystem = fileSystem;
        _log = log;
        var dir = Path.Combine(paths.DataPath, "EmbyTMDBScraperFix");
        Directory.CreateDirectory(dir);
        _stateFilePath = Path.Combine(dir, "pending-changes.json");
        LoadPendingChanges();
    }

    public void ApplyConfiguration(PluginConfiguration cfg)
    {
        lock (_gate)
        {
            DisposeWatchers_NoLock();
            if (!cfg.AutoScanEnabled)
            {
                SavePendingChanges_NoLock();
                return;
            }

            foreach (var root in GetEnabledRoots(cfg))
            {
                if (!Directory.Exists(root.Path))
                {
                    _log.Warn($"Library path not found for watcher: {root.Path}");
                    continue;
                }

                try
                {
                    var watcher = new FileSystemWatcher(root.Path)
                    {
                        IncludeSubdirectories = true,
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size,
                        Filter = "*.*",
                        EnableRaisingEvents = true
                    };
                    watcher.Created += OnCreated;
                    watcher.Changed += OnChanged;
                    watcher.Deleted += OnDeleted;
                    watcher.Renamed += OnRenamed;
                    watcher.Error += OnWatcherError;
                    _watchers[root.Path] = watcher;
                }
                catch (Exception ex)
                {
                    _log.Error($"Failed to start watcher for {root.Path}", ex);
                }
            }

            _log.Info($"Event-driven watchers active. Count={_watchers.Count}");
            SavePendingChanges_NoLock();
        }
    }

    public async Task<IncrementalScanResult> RunOnceAsync(PluginConfiguration cfg, CancellationToken cancellationToken)
    {
        if (_running) return new IncrementalScanResult { Message = "Previous scan is still running; skipped." };
        _running = true;
        try
        {
            var result = new IncrementalScanResult { StartedAt = DateTimeOffset.Now };
            if (!cfg.AutoScanEnabled) { result.Message = "Auto scan disabled."; return result; }

            List<ChangeEntry> batch;
            lock (_gate)
            {
                batch = _pendingChanges.Values.OrderBy(x => x.TimestampUtc).ToList();
                _pendingChanges.Clear();
                SavePendingChanges_NoLock();
            }

            foreach (var change in batch)
            {
                cancellationToken.ThrowIfCancellationRequested();
                switch (change.Kind)
                {
                    case ChangeKind.Deleted:
                        result.Deleted.Add(change.Path);
                        break;
                    case ChangeKind.Modified:
                        result.Modified.Add(change.Path);
                        break;
                    default:
                        result.Added.Add(change.Path);
                        break;
                }
            }

            Deduplicate(result);

            if (result.HasChanges)
            {
                _log.Info($"Media changes detected from watcher queue. Added={result.Added.Count}, Modified={result.Modified.Count}, Deleted={result.Deleted.Count}");
                _libraryManager.QueueLibraryScan();
                if (cfg.AutoMetadataRefresh)
                {
                    await TriggerMetadataRefreshAsync(result, cfg, cancellationToken).ConfigureAwait(false);
                }
            }
            else
            {
                _log.Info("Incremental scan finished. No watcher-reported file changes detected.");
            }

            result.FinishedAt = DateTimeOffset.Now;
            result.Message = "Completed";
            return result;
        }
        catch (Exception ex)
        {
            _log.Error("Incremental scan failed.", ex);
            return new IncrementalScanResult { Message = ex.Message, FinishedAt = DateTimeOffset.Now };
        }
        finally
        {
            _running = false;
        }
    }

    private void OnCreated(object sender, FileSystemEventArgs e) => RecordChange(e.FullPath, ChangeKind.Added);
    private void OnChanged(object sender, FileSystemEventArgs e) => RecordChange(e.FullPath, ChangeKind.Modified);
    private void OnDeleted(object sender, FileSystemEventArgs e) => RecordChange(e.FullPath, ChangeKind.Deleted);

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        RecordChange(e.OldFullPath, ChangeKind.Deleted);
        RecordChange(e.FullPath, ChangeKind.Added);
    }

    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        _log.Error("FileSystemWatcher error.", e.GetException());
    }

    private void RecordChange(string path, ChangeKind kind)
    {
        if (!ShouldTrackPath(path, kind == ChangeKind.Deleted)) return;

        lock (_gate)
        {
            _pendingChanges[path] = new ChangeEntry(path, kind, DateTime.UtcNow);
            TrimPendingChanges_NoLock();
            SavePendingChanges_NoLock();
        }
    }

    private static bool ShouldTrackPath(string path, bool allowMissing)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        if (Directory.Exists(path)) return false;
        if (!allowMissing && !File.Exists(path)) return false;
        return MediaExtensions.Contains(Path.GetExtension(path));
    }

    private async Task TriggerMetadataRefreshAsync(IncrementalScanResult result, PluginConfiguration cfg, CancellationToken cancellationToken)
    {
        var changed = result.Added.Concat(result.Modified).Distinct(StringComparer.OrdinalIgnoreCase).Take(500).ToList();
        foreach (var file in changed)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var item = _libraryManager.FindByPath(file, false) ?? _libraryManager.FindByPath(Path.GetDirectoryName(file) ?? file, false);
                if (item == null) { _log.Info($"Metadata refresh deferred until Emby indexes file: {file}"); continue; }
                var options = new MetadataRefreshOptions(_fileSystem)
                {
                    MetadataRefreshMode = MetadataRefreshMode.FullRefresh,
                    ImageRefreshMode = MetadataRefreshMode.FullRefresh,
                    ReplaceAllMetadata = false,
                    EnableRemoteContentProbe = true,
                    IsAutomated = true,
                    Recursive = false,
                    EnableSubtitleDownloading = true
                };
                await _providerManager.RefreshFullItem(item, options, cancellationToken).ConfigureAwait(false);
                _log.Info($"Metadata refresh completed: {item.Name} ({file})");
            }
            catch (Exception ex)
            {
                _log.Warn($"Metadata refresh failed and will be retried by later change events: {file}; {ex.Message}");
            }
        }
    }

    private IEnumerable<LibraryRoot> GetEnabledRoots(PluginConfiguration cfg)
    {
        foreach (var lib in cfg.Libraries.Where(x => x.Enabled && !string.IsNullOrWhiteSpace(x.Path)))
        {
            yield return new LibraryRoot(lib.Name, lib.Path);
        }
    }

    private static void Deduplicate(IncrementalScanResult result)
    {
        result.Added = result.Added.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        result.Modified = result.Modified
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(x => !result.Added.Contains(x, StringComparer.OrdinalIgnoreCase))
            .ToList();
        result.Deleted = result.Deleted
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(x => !result.Added.Contains(x, StringComparer.OrdinalIgnoreCase))
            .ToList();
    }

    private void LoadPendingChanges()
    {
        lock (_gate)
        {
            try
            {
                if (!File.Exists(_stateFilePath)) return;
                var json = File.ReadAllText(_stateFilePath);
                var items = JsonSerializer.Deserialize<List<ChangeEntry>>(json) ?? new List<ChangeEntry>();
                foreach (var item in items)
                {
                    _pendingChanges[item.Path] = item;
                }
                TrimPendingChanges_NoLock();
            }
            catch (Exception ex)
            {
                _log.Error("Failed to load pending scan changes.", ex);
            }
        }
    }

    private void SavePendingChanges_NoLock()
    {
        try
        {
            var json = JsonSerializer.Serialize(_pendingChanges.Values.OrderBy(x => x.TimestampUtc).ToList());
            File.WriteAllText(_stateFilePath, json);
        }
        catch (Exception ex)
        {
            _log.Error("Failed to save pending scan changes.", ex);
        }
    }

    private void TrimPendingChanges_NoLock()
    {
        if (_pendingChanges.Count <= MaxTrackedEvents) return;
        foreach (var stale in _pendingChanges.Values.OrderBy(x => x.TimestampUtc).Take(_pendingChanges.Count - MaxTrackedEvents).ToList())
        {
            _pendingChanges.Remove(stale.Path);
        }
    }

    private void DisposeWatchers_NoLock()
    {
        foreach (var watcher in _watchers.Values)
        {
            try
            {
                watcher.EnableRaisingEvents = false;
                watcher.Created -= OnCreated;
                watcher.Changed -= OnChanged;
                watcher.Deleted -= OnDeleted;
                watcher.Renamed -= OnRenamed;
                watcher.Error -= OnWatcherError;
                watcher.Dispose();
            }
            catch
            {
            }
        }
        _watchers.Clear();
    }

    public void Dispose()
    {
        lock (_gate)
        {
            DisposeWatchers_NoLock();
            SavePendingChanges_NoLock();
        }
    }

    private sealed class LibraryRoot
    {
        public string Name { get; }
        public string Path { get; }

        public LibraryRoot(string name, string path)
        {
            Name = name;
            Path = path;
        }
    }
}

public sealed class IncrementalScanResult
{
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset FinishedAt { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> Added { get; set; } = new();
    public List<string> Modified { get; set; } = new();
    public List<string> Deleted { get; set; } = new();
    public bool HasChanges => Added.Count > 0 || Modified.Count > 0 || Deleted.Count > 0;
}

public sealed class ChangeEntry
{
    public ChangeEntry()
    {
    }

    public ChangeEntry(string path, ChangeKind kind, DateTime timestampUtc)
    {
        Path = path;
        Kind = kind;
        TimestampUtc = timestampUtc;
    }

    public string Path { get; set; } = string.Empty;
    public ChangeKind Kind { get; set; }
    public DateTime TimestampUtc { get; set; }
}

public enum ChangeKind
{
    Added = 1,
    Modified = 2,
    Deleted = 3
}
