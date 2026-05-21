using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EmbyTMDBScraperFix.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.IO;

namespace EmbyTMDBScraperFix.Services;

public sealed class IncrementalScanService
{
    private readonly ILibraryManager _libraryManager;
    private readonly IProviderManager _providerManager;
    private readonly IFileSystem _fileSystem;
    private readonly PluginLogService _log;
    private readonly Dictionary<string, FileSnapshot> _snapshot = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _retryCount = new(StringComparer.OrdinalIgnoreCase);
    private volatile bool _running;

    private static readonly HashSet<string> MediaExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mkv", ".mp4", ".avi", ".mov", ".wmv", ".m4v", ".ts", ".m2ts", ".flv", ".webm",
        ".mp3", ".flac", ".aac", ".m4a", ".wav", ".ape", ".ogg",
        ".nfo", ".srt", ".ass", ".ssa", ".sub"
    };

    public IncrementalScanService(ILibraryManager libraryManager, IProviderManager providerManager, IFileSystem fileSystem, PluginLogService log)
    {
        _libraryManager = libraryManager;
        _providerManager = providerManager;
        _fileSystem = fileSystem;
        _log = log;
    }

    public async Task<IncrementalScanResult> RunOnceAsync(PluginConfiguration cfg, CancellationToken cancellationToken)
    {
        if (_running) return new IncrementalScanResult { Message = "Previous scan is still running; skipped." };
        _running = true;
        try
        {
            var result = new IncrementalScanResult { StartedAt = DateTimeOffset.Now };
            if (!cfg.AutoScanEnabled) { result.Message = "Auto scan disabled."; return result; }

            var roots = GetEnabledRoots(cfg).ToList();
            _log.Info($"Incremental scan started. Enabled root count: {roots.Count}");
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var root in roots)
            {
                if (!Directory.Exists(root.Path)) { _log.Warn($"Library path not found: {root.Path}"); continue; }
                foreach (var file in EnumerateMediaFiles(root.Path, cancellationToken))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    seen.Add(file);
                    var info = new FileInfo(file);
                    var current = new FileSnapshot(info.FullName, info.Length, info.LastWriteTimeUtc);
                    if (!_snapshot.TryGetValue(file, out var previous)) { _snapshot[file] = current; result.Added.Add(file); }
                    else if (previous.Length != current.Length || previous.LastWriteUtc != current.LastWriteUtc) { _snapshot[file] = current; result.Modified.Add(file); }
                }
            }

            foreach (var known in _snapshot.Keys.ToArray())
            {
                if (!seen.Contains(known)) { _snapshot.Remove(known); result.Deleted.Add(known); }
            }

            if (result.HasChanges)
            {
                _log.Info($"Media changes detected. Added={result.Added.Count}, Modified={result.Modified.Count}, Deleted={result.Deleted.Count}");
                _libraryManager.QueueLibraryScan();
                if (cfg.AutoMetadataRefresh)
                {
                    await TriggerMetadataRefreshAsync(result, cfg, cancellationToken).ConfigureAwait(false);
                }
            }
            else _log.Info("Incremental scan finished. No file changes detected.");

            result.FinishedAt = DateTimeOffset.Now;
            result.Message = "Completed";
            return result;
        }
        catch (Exception ex)
        {
            _log.Error("Incremental scan failed.", ex);
            return new IncrementalScanResult { Message = ex.Message, FinishedAt = DateTimeOffset.Now };
        }
        finally { _running = false; }
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
                _retryCount.Remove(file);
                _log.Info($"Metadata refresh completed: {item.Name} ({file})");
            }
            catch (Exception ex)
            {
                var count = _retryCount.TryGetValue(file, out var old) ? old + 1 : 1;
                _retryCount[file] = count;
                if (count <= cfg.MaxScrapeRetryCount) _log.Warn($"Metadata refresh failed; will retry next scan ({count}/{cfg.MaxScrapeRetryCount}): {file}; {ex.Message}");
                else _log.Error($"Metadata refresh failed beyond retry limit: {file}", ex);
            }
        }
    }

    private IEnumerable<LibraryRoot> GetEnabledRoots(PluginConfiguration cfg)
    {
        foreach (var lib in cfg.Libraries.Where(x => x.Enabled && !string.IsNullOrWhiteSpace(x.Path))) yield return new LibraryRoot(lib.Name, lib.Path);
    }

    private static IEnumerable<string> EnumerateMediaFiles(string root, CancellationToken cancellationToken)
    {
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var dir = pending.Pop();
            string[] dirs; string[] files;
            try { dirs = Directory.GetDirectories(dir); files = Directory.GetFiles(dir); }
            catch { continue; }
            foreach (var child in dirs) pending.Push(child);
            foreach (var file in files) if (MediaExtensions.Contains(Path.GetExtension(file))) yield return file;
        }
    }

    private sealed class FileSnapshot
    {
        public string Path { get; }
        public long Length { get; }
        public DateTime LastWriteUtc { get; }

        public FileSnapshot(string path, long length, DateTime lastWriteUtc)
        {
            Path = path;
            Length = length;
            LastWriteUtc = lastWriteUtc;
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
    public List<string> Added { get; } = new();
    public List<string> Modified { get; } = new();
    public List<string> Deleted { get; } = new();
    public bool HasChanges => Added.Count > 0 || Modified.Count > 0 || Deleted.Count > 0;
}
