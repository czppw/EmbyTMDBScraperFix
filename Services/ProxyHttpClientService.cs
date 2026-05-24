using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
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
        var targets = new List<ProxyTestTarget>
        {
            new ProxyTestTarget("TMDB 图片域名", "https://image.tmdb.org/t/p/w92/wwemzKWzjKYJFfCeiB57q3r4Bcm.png", false, false)
        };

        var tmdbApiBaseUrl = TmdbUrlHelper.ResolveApiBaseUrl(cfg.TmdbApiBaseUrl);
        var tmdbConfigurationUrl = tmdbApiBaseUrl + "/configuration";
        var tmdbApiUrl = string.IsNullOrWhiteSpace(cfg.TmdbApiKey)
            ? tmdbConfigurationUrl
            : tmdbConfigurationUrl + "?api_key=" + Uri.EscapeDataString(cfg.TmdbApiKey);
        targets.Add(new ProxyTestTarget("TMDB API", tmdbApiUrl, true, true));

        foreach (var target in targets)
        {
            var uri = new Uri(target.Url);
            try
            {
                using var response = await SendAsync(uri, cfg, TimeSpan.FromSeconds(45), HttpCompletionOption.ResponseHeadersRead, target.ExpectsJson, cancellationToken).ConfigureAwait(false);
                result.Items.Add(BuildSuccessItem(target, uri, cfg, response.StatusCode));
            }
            catch (Exception ex)
            {
                result.Items.Add(new ProxyTestItem
                {
                    Name = target.Name,
                    Url = TmdbUrlHelper.SanitizeUrl(target.Url),
                    Proxied = _policy.ShouldProxy(uri, cfg),
                    Reachable = false,
                    Success = false,
                    StatusCode = 0,
                    Message = ex.Message
                });
                _log.Warn($"Proxy test failed for {TmdbUrlHelper.SanitizeUrl(target.Url)}: {ex.Message}");
            }
        }

        var apiItem = result.Items.Find(x => string.Equals(x.Name, "TMDB API", StringComparison.Ordinal));
        var imageItem = result.Items.Find(x => string.Equals(x.Name, "TMDB 图片域名", StringComparison.Ordinal));

        if (imageItem?.Reachable != true)
        {
            result.Success = false;
            result.Summary = "代理没有连通到 TMDB 图片域名，当前代理不可用于 TMDB 刮削。";
        }
        else if (apiItem?.Reachable != true)
        {
            result.Success = false;
            result.Summary = "代理没有连通到 TMDB API 域名，当前代理不可用于 TMDB 刮削。当前版本已在 HttpClient 失败时自动回退到 HttpWebRequest。";
        }
        else if (string.IsNullOrWhiteSpace(cfg.TmdbApiKey))
        {
            result.Success = false;
            result.Summary = "代理连通正常，但未配置 TMDB API Key。插件自己的 TMDB 刮削器不会返回元数据。";
        }
        else if (apiItem?.StatusCode == 401)
        {
            result.Success = false;
            result.Summary = "代理连通正常，但 TMDB API Key 无效或未授权，TMDB 刮削仍会失败。";
        }
        else if (apiItem?.Success == true && imageItem.Success)
        {
            result.Success = true;
            result.Summary = "代理连通正常，TMDB API 与图片域名都可访问。";
        }
        else
        {
            result.Success = false;
            result.Summary = "代理测试未通过，请检查代理配置、网络和 TMDB API Key。";
        }

        return result;
    }

    public async Task<Stream> GetStreamAsync(Uri uri, PluginConfiguration cfg, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await SendAsync(uri, cfg, TimeSpan.FromSeconds(45), HttpCompletionOption.ResponseContentRead, true, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            return new MemoryStream(bytes, writable: false);
        }
        catch (Exception ex) when (ShouldFallbackToWebRequest(uri, cfg, ex))
        {
            _log.Warn($"HttpClient TMDB request failed, retrying with HttpWebRequest: {TmdbUrlHelper.SanitizeUrl(uri.ToString())}. {ex.Message}");
            return await GetStreamViaWebRequestAsync(uri, cfg, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<HttpResponseInfo> GetResponseInfoAsync(string url, PluginConfiguration cfg, CancellationToken cancellationToken)
    {
        var uri = new Uri(url);
        try
        {
            using var response = await SendAsync(uri, cfg, TimeSpan.FromSeconds(45), HttpCompletionOption.ResponseContentRead, false, cancellationToken).ConfigureAwait(false);
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
        catch (Exception ex) when (ShouldFallbackToWebRequest(uri, cfg, ex))
        {
            _log.Warn($"HttpClient image/info request failed, retrying with HttpWebRequest: {uri}. {ex.Message}");
            return await GetResponseInfoViaWebRequestAsync(uri, cfg, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<HttpResponseMessage> SendAsync(Uri uri, PluginConfiguration cfg, TimeSpan timeout, HttpCompletionOption completionOption, bool expectsJson, CancellationToken cancellationToken)
    {
        using var client = CreateHttpClient(uri, cfg, timeout);
        using var request = CreateRequest(uri, expectsJson);
        return await client.SendAsync(request, completionOption, cancellationToken).ConfigureAwait(false);
    }

    private HttpRequestMessage CreateRequest(Uri uri, bool expectsJson)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Version = HttpVersion.Version11;
        request.Headers.UserAgent.Clear();
        request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (compatible; EmbyTMDBScraperFix/1.0; +https://github.com/czppw/EmbyTMDBScraperFix)");
        request.Headers.Accept.Clear();
        if (expectsJson)
        {
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.TryAddWithoutValidation("Accept", "application/json, text/plain, */*");
        }
        else
        {
            request.Headers.TryAddWithoutValidation("Accept", "*/*");
        }
        request.Headers.TryAddWithoutValidation("Accept-Language", "zh-CN,zh;q=0.9,en;q=0.8");
        request.Headers.ConnectionClose = true;
        return request;
    }

    private async Task<Stream> GetStreamViaWebRequestAsync(Uri uri, PluginConfiguration cfg, CancellationToken cancellationToken)
    {
#pragma warning disable SYSLIB0014
        var request = (HttpWebRequest)WebRequest.Create(uri);
#pragma warning restore SYSLIB0014
        ConfigureWebRequest(request, uri, cfg, expectsJson: true);
        using (cancellationToken.Register(() => request.Abort()))
        using (var response = (HttpWebResponse)await request.GetResponseAsync().ConfigureAwait(false))
        {
            var stream = CreateResponseStream(response);
            using (stream)
            using (var memory = new MemoryStream())
            {
                await stream.CopyToAsync(memory).ConfigureAwait(false);
                return new MemoryStream(memory.ToArray(), writable: false);
            }
        }
    }

    private async Task<HttpResponseInfo> GetResponseInfoViaWebRequestAsync(Uri uri, PluginConfiguration cfg, CancellationToken cancellationToken)
    {
#pragma warning disable SYSLIB0014
        var request = (HttpWebRequest)WebRequest.Create(uri);
#pragma warning restore SYSLIB0014
        ConfigureWebRequest(request, uri, cfg, expectsJson: false);
        using (cancellationToken.Register(() => request.Abort()))
        using (var response = (HttpWebResponse)await request.GetResponseAsync().ConfigureAwait(false))
        using (var stream = CreateResponseStream(response))
        using (var memory = new MemoryStream())
        {
            await stream.CopyToAsync(memory).ConfigureAwait(false);
            var bytes = memory.ToArray();
            return new HttpResponseInfo
            {
                Content = new MemoryStream(bytes, writable: false),
                ContentType = response.ContentType,
                StatusCode = response.StatusCode,
                ContentLength = bytes.Length,
                ResponseUrl = uri.ToString()
            };
        }
    }

    private void ConfigureWebRequest(HttpWebRequest request, Uri uri, PluginConfiguration cfg, bool expectsJson)
    {
        request.Method = "GET";
        request.ProtocolVersion = HttpVersion.Version11;
        request.KeepAlive = false;
        request.Timeout = 45000;
        request.ReadWriteTimeout = 45000;
        request.UserAgent = "Mozilla/5.0 (compatible; EmbyTMDBScraperFix/1.0; +https://github.com/czppw/EmbyTMDBScraperFix)";
        request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
        request.Headers[HttpRequestHeader.AcceptLanguage] = "zh-CN,zh;q=0.9,en;q=0.8";
        request.Accept = expectsJson ? "application/json, text/plain, */*" : "*/*";

        if (_policy.ShouldProxy(uri, cfg))
        {
            request.Proxy = _policy.CreateProxy(cfg);
            _log.Info($"TMDB request uses configured HTTP proxy via HttpWebRequest: {uri.Host}");
        }
        else
        {
            request.Proxy = null;
        }
    }

    private static Stream CreateResponseStream(HttpWebResponse response)
    {
        var stream = response.GetResponseStream() ?? Stream.Null;
        var encoding = response.ContentEncoding ?? string.Empty;
        if (encoding.IndexOf("gzip", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return new GZipStream(stream, CompressionMode.Decompress);
        }
        if (encoding.IndexOf("deflate", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return new DeflateStream(stream, CompressionMode.Decompress);
        }
        return stream;
    }

    private bool ShouldFallbackToWebRequest(Uri uri, PluginConfiguration cfg, Exception ex)
    {
        if (!_policy.ShouldProxy(uri, cfg)) return false;
        if (!TmdbUrlHelper.IsTmdbApiHost(uri.Host, cfg.TmdbApiBaseUrl)) return false;
        return ex is TaskCanceledException || ex is TimeoutException || ex is HttpRequestException || ex is IOException || ex is WebException;
    }

    private ProxyTestItem BuildSuccessItem(ProxyTestTarget target, Uri uri, PluginConfiguration cfg, HttpStatusCode statusCode)
    {
        var proxied = _policy.ShouldProxy(uri, cfg);
        var code = (int)statusCode;
        var reachable = true;
        var success = code >= 200 && code < 300;
        var message = $"HTTP {code}";

        if (string.Equals(target.Name, "TMDB API", StringComparison.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(cfg.TmdbApiKey) && code == 401)
            {
                reachable = true;
                success = false;
                message = "已连通 TMDB API，但未配置 TMDB API Key。";
            }
            else if (!string.IsNullOrWhiteSpace(cfg.TmdbApiKey) && code == 401)
            {
                reachable = true;
                success = false;
                message = "已连通 TMDB API，但 TMDB API Key 无效或未授权。";
            }
            else if (success)
            {
                message = "TMDB API 可访问。";
            }
        }
        else if (string.Equals(target.Name, "TMDB 图片域名", StringComparison.Ordinal) && success)
        {
            message = "TMDB 图片域名可访问。";
        }

        return new ProxyTestItem
        {
            Name = target.Name,
            Url = TmdbUrlHelper.SanitizeUrl(target.Url),
            Proxied = proxied,
            Reachable = reachable,
            Success = success,
            StatusCode = code,
            Message = message
        };
    }

    private HttpClient CreateHttpClient(Uri uri, PluginConfiguration cfg, TimeSpan timeout)
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            UseCookies = false
        };

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

internal sealed class ProxyTestTarget
{
    public ProxyTestTarget(string name, string url, bool requiresApiKey, bool expectsJson)
    {
        Name = name;
        Url = url;
        RequiresApiKey = requiresApiKey;
        ExpectsJson = expectsJson;
    }

    public string Name { get; }
    public string Url { get; }
    public bool RequiresApiKey { get; }
    public bool ExpectsJson { get; }
}

public sealed class ProxyTestResult
{
    public bool Success { get; set; }
    public string Summary { get; set; } = string.Empty;
    public List<ProxyTestItem> Items { get; set; } = new();
}

public sealed class ProxyTestItem
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public bool Proxied { get; set; }
    public bool Reachable { get; set; }
    public bool Success { get; set; }
    public int StatusCode { get; set; }
    public string Message { get; set; } = string.Empty;
}
