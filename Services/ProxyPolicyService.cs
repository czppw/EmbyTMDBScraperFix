using System;
using System.Linq;
using System.Net;
using EmbyTMDBScraperFix.Configuration;

namespace EmbyTMDBScraperFix.Services;

public sealed class ProxyPolicyService
{
    private readonly PluginLogService _log;
    private IWebProxy? _previousDefaultProxy;
    private bool _globalHookInstalled;

    public ProxyPolicyService(PluginLogService log) => _log = log;

    public bool ShouldProxy(Uri uri, PluginConfiguration cfg)
    {
        if (!cfg.ProxyEnabled || string.IsNullOrWhiteSpace(cfg.ProxyHost) || cfg.ProxyPort <= 0) return false;
        if (!uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) && !uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) return false;
        if (IsLocalOrPrivate(uri.Host)) return false;
        return cfg.MetadataProxyHosts.Any(host => HostMatches(uri.Host, host));
    }

    public WebProxy? CreateProxy(PluginConfiguration cfg)
    {
        if (!cfg.ProxyEnabled || string.IsNullOrWhiteSpace(cfg.ProxyHost) || cfg.ProxyPort <= 0) return null;
        var proxy = new WebProxy(cfg.ProxyHost, cfg.ProxyPort) { BypassProxyOnLocal = true };
        if (!string.IsNullOrWhiteSpace(cfg.ProxyUsername)) proxy.Credentials = new NetworkCredential(cfg.ProxyUsername, cfg.ProxyPassword ?? string.Empty);
        return proxy;
    }

    public void ApplyRiskyGlobalHookIfRequested(PluginConfiguration cfg)
    {
        if (!cfg.EnableLegacyGlobalProxyHook || !cfg.ProxyEnabled) { RemoveRiskyGlobalHook(); return; }
        var proxy = CreateProxy(cfg);
        if (proxy == null) return;
#pragma warning disable SYSLIB0014
        if (!_globalHookInstalled) _previousDefaultProxy = WebRequest.DefaultWebProxy;
        WebRequest.DefaultWebProxy = proxy;
#pragma warning restore SYSLIB0014
        _globalHookInstalled = true;
        _log.Warn("High-risk global proxy hook installed.");
    }

    public void RemoveRiskyGlobalHook()
    {
        if (!_globalHookInstalled) return;
#pragma warning disable SYSLIB0014
        WebRequest.DefaultWebProxy = _previousDefaultProxy;
#pragma warning restore SYSLIB0014
        _globalHookInstalled = false;
        _log.Info("High-risk global proxy hook removed.");
    }

    private static bool HostMatches(string actual, string configured)
    {
        actual = actual.Trim().ToLowerInvariant();
        configured = configured.Trim().ToLowerInvariant();
        return actual == configured || actual.EndsWith("." + configured, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLocalOrPrivate(string host)
    {
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)) return true;
        if (IPAddress.TryParse(host, out var ip))
        {
            if (IPAddress.IsLoopback(ip)) return true;
            var b = ip.GetAddressBytes();
            if (b.Length == 4)
            {
                return b[0] == 10 || (b[0] == 172 && b[1] >= 16 && b[1] <= 31) || (b[0] == 192 && b[1] == 168) || (b[0] == 169 && b[1] == 254);
            }
        }
        return false;
    }
}
