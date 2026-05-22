using System;
using System.Collections.Generic;
using MediaBrowser.Model.Tasks;

namespace EmbyTMDBScraperFix.Tasks;

public sealed class AutoIncrementalScanTask : IScheduledTask
{
    private readonly PluginRuntime _runtime;

    public AutoIncrementalScanTask(PluginRuntime runtime)
    {
        _runtime = runtime;
    }

    public string Name => "EmbyTMDBScraperFix Auto Incremental Scan";

    public string Key => "EmbyTMDBScraperFixAutoIncrementalScan";

    public string Description => "Runs configurable incremental scan and metadata refresh.";

    public string Category => "Library";

    public async System.Threading.Tasks.Task Execute(System.Threading.CancellationToken cancellationToken, IProgress<double> progress)
    {
        progress.Report(0);
        await _runtime.RunScanAsync(cancellationToken).ConfigureAwait(false);
        progress.Report(100);
    }

    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        return Array.Empty<TaskTriggerInfo>();
    }
}


