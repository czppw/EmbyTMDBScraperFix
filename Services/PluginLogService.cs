using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MediaBrowser.Common.Configuration;

namespace EmbyTMDBScraperFix.Services;

public sealed class PluginLogService
{
    private readonly string _logFile;
    private readonly ConcurrentQueue<PluginLogEntry> _entries = new();
    private const int MaxMemoryEntries = 500;

    public PluginLogService(IApplicationPaths paths)
    {
        var dir = Path.Combine(paths.DataPath, "EmbyTMDBScraperFix");
        Directory.CreateDirectory(dir);
        _logFile = Path.Combine(dir, "plugin.log");
    }

    public void Info(string message) => Write("INFO", message);
    public void Warn(string message) => Write("WARN", message);
    public void Error(string message, Exception? ex = null) => Write("ERROR", ex == null ? message : message + "\n" + ex);
    public IReadOnlyList<PluginLogEntry> GetRecent(int count = 200)
    {
        var all = _entries.ToArray();
        if (all.Length <= count) return all;
        var result = new PluginLogEntry[count];
        Array.Copy(all, all.Length - count, result, 0, count);
        return result;
    }

    private void Write(string level, string message)
    {
        var entry = new PluginLogEntry { Time = DateTimeOffset.Now, Level = level, Message = message };
        _entries.Enqueue(entry);
        while (_entries.Count > MaxMemoryEntries && _entries.TryDequeue(out _)) { }
        try { File.AppendAllText(_logFile, $"{entry.Time:O} [{entry.Level}] {entry.Message}{Environment.NewLine}", Encoding.UTF8); } catch { }
    }
}

public sealed class PluginLogEntry
{
    public DateTimeOffset Time { get; set; }
    public string Level { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
