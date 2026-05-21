using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using EmbyTMDBScraperFix.Configuration;
using MediaBrowser.Common.Net;

namespace EmbyTMDBScraperFix.Services;

public sealed class ProxyHttpClientService
{
    private readonly ProxyPolicyService _policy;
    private readonly PluginLogService _log;

    public ProxyHttpClientService(ProxyPolicyService policy, PluginLogService log)
    {
        _policy = policy;
        _log = log;
    }

    public async Task<ProxyTestResult> TestProxyAsync(PluginConfiguration cfg, CancellationToken cancellationToken)
    {
        var result = new ProxyTestResult();
        var targets = new[] { "https://api.themoviedb.org/3/configuration", "https://image.tmdb.org/t/p/w92/wwemzKWzjKYJFfCeiB57q3r4Bcm.png" };
        foreach (var target in targets)
        {
            try
            {
                var uri = new Uri(target);
                using var client = CreateHttpClient(uri, cfg, TimeSpan.FromSeconds(10));
                using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                request.Headers.UserAgent.ParseAdd("EmbyTMDBScraperFix/1.0");
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                result.Items.Add(new ProxyTestItem { Url = target, Proxied = _policy.ShouldProxy(uri, cfg), Success = true, StatusCode = (int)response.StatusCode, Message = response.ReasonPhrase ?? "OK" });
            }
            catch (Exception ex)
            {
                result.Items.Add(new ProxyTestItem { Url = target, Proxied = true, Success = false, Message = ex.Message });
                _log.Warn($"Proxy test failed for {target}: {ex.Message}");
            }
        }
        result.Success = result.Items.Exists(x => x.Success);
        return result;
    }

    public async Task<Stream> GetStreamAsync(Uri uri, PluginConfiguration cfg, CancellationToken cancellationToken)
    {
        using var client = CreateHttpClient(uri, cfg, TimeSpan.FromSeconds(30));
        var bytes = await client.GetByteArrayAsync(uri).ConfigureAwait(false);
        return new MemoryStream(bytes, writable: false);
    }

    public async Task<HttpResponseInfo> GetResponseInfoAsync(string url, PluginConfiguration cfg, CancellationToken cancellationToken)
    {
        var uri = new Uri(url);
        using var client = CreateHttpClient(uri, cfg, TimeSpan.FromSeconds(30));
        using var response = await client.GetAsync(uri, cancellationToken).ConfigureAwait(false);
        var bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
        return new HttpResponseInfo
        {
            Content = new MemoryStream(bytes, writable: false),
            ContentType = response.Content.Headers.ContentType?.MediaType,
            StatusCode = response.StatusCode,
            ContentLength = bytes.Length,
            ResponseUrl = url
        };
    }

    private HttpClient CreateHttpClient(Uri uri, PluginConfiguration cfg, TimeSpan timeout)
    {
        var handler = new HttpClientHandler();
        if (_policy.ShouldProxy(uri, cfg))
        {
            handler.UseProxy = true;
            handler.Proxy = _policy.CreateProxy(cfg);
            _log.Info($"TMDB request uses configured HTTP proxy: {uri.Host}");
        }
        else
        {
            handler.UseProxy = false;
        }
        return new HttpClient(handler, disposeHandler: true) { Timeout = timeout };
    }
}

public sealed class ProxyTestResult
{
    public bool Success { get; set; }
    public System.Collections.Generic.List<ProxyTestItem> Items { get; set; } = new();
}

public sealed class ProxyTestItem
{
    public string Url { get; set; } = string.Empty;
    public bool Proxied { get; set; }
    public bool Success { get; set; }
    public int StatusCode { get; set; }
    public string Message { get; set; } = string.Empty;
}
