using System;
using System.Threading;
using System.Threading.Tasks;
using EmbyTMDBScraperFix.Configuration;
using EmbyTMDBScraperFix.Services;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.IO;

namespace EmbyTMDBScraperFix;

public sealed class PluginRuntime : IServerEntryPoint
{
    private static PluginRuntime? _instance;
    private readonly Timer _timer;
    private readonly ProxyPolicyService _proxyPolicy;
    private readonly ProxyHttpClientService _proxyClient;
    private readonly IncrementalScanService _scanService;
    private readonly PluginLogService _log;

    public PluginRuntime(IApplicationPaths paths, ILibraryManager libraryManager, IProviderManager providerManager, IFileSystem fileSystem)
    {
        _log = new PluginLogService(paths);
        _proxyPolicy = new ProxyPolicyService(_log);
        _proxyClient = new ProxyHttpClientService(_proxyPolicy, _log);
        _scanService = new IncrementalScanService(libraryManager, providerManager, fileSystem, _log);
        _timer = new Timer(OnTimer, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _instance = this;
    }

    public static PluginRuntime Instance => _instance ?? throw new InvalidOperationException("Plugin runtime has not started yet.");

    public static bool TryGetInstance(out PluginRuntime runtime)
    {
        runtime = _instance!;
        return runtime != null;
    }

    public PluginConfiguration Configuration => Plugin.Instance?.Configuration ?? new PluginConfiguration();
    public PluginLogService Log => _log;
    public ProxyHttpClientService ProxyClient => _proxyClient;
    public IncrementalScanService ScanService => _scanService;

    public void Run()
    {
        _log.Info("EmbyTMDBScraperFix runtime started.");
        ApplyConfiguration();
    }

    public void ApplyConfiguration()
    {
        var cfg = Configuration;
        _proxyPolicy.ApplyRiskyGlobalHookIfRequested(cfg);
        var interval = TimeSpan.FromMinutes(Math.Max(1, cfg.ScanIntervalMinutes));
        if (cfg.AutoScanEnabled)
        {
            _timer.Change(TimeSpan.FromMinutes(1), interval);
            _log.Info($"Auto incremental scan enabled. Interval={interval.TotalMinutes} minutes.");
        }
        else
        {
            _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            _log.Info("Auto incremental scan disabled.");
        }
    }

    public Task<IncrementalScanResult> RunScanAsync(CancellationToken cancellationToken) => _scanService.RunOnceAsync(Configuration, cancellationToken);

    private async void OnTimer(object? state)
    {
        try { await RunScanAsync(CancellationToken.None).ConfigureAwait(false); }
        catch (Exception ex) { _log.Error("Timer triggered scan failed.", ex); }
    }

    public void Dispose()
    {
        _timer.Dispose();
        _proxyPolicy.RemoveRiskyGlobalHook();
        _log.Info("EmbyTMDBScraperFix runtime disposed.");
    }
}
