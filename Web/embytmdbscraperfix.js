define(['loading', 'emby-input', 'emby-button', 'emby-checkbox'], function (loading) {
    'use strict';

    var pluginId = 'b6b08b4b-1b7d-4f1a-9d6c-f1f6b1ef0a11';

    function qs(view, selector) {
        return view.querySelector(selector);
    }

    function setStatus(view, message, isError) {
        var el = qs(view, '#status');
        if (!el) {
            return;
        }

        el.style.color = isError ? '#c00' : '';
        el.textContent = message || '';
    }

    function setBool(view, id, value) {
        qs(view, '#' + id).checked = !!value;
    }

    function getBool(view, id) {
        return !!qs(view, '#' + id).checked;
    }

    function intValue(view, id, fallback) {
        var value = parseInt(qs(view, '#' + id).value, 10);
        return isNaN(value) ? fallback : value;
    }

    function api(path, method, body) {
        var options = {
            type: method || 'GET',
            url: path,
            dataType: 'json'
        };

        if (body !== undefined) {
            options.contentType = 'application/json';
            options.data = JSON.stringify(body);
        }

        return ApiClient.ajax(options);
    }

    function fillForm(view, cfg) {
        cfg = cfg || {};
        setBool(view, 'proxyEnabled', cfg.ProxyEnabled);
        qs(view, '#proxyHost').value = cfg.ProxyHost || '';
        qs(view, '#proxyPort').value = cfg.ProxyPort || 7890;
        qs(view, '#proxyUsername').value = cfg.ProxyUsername || '';
        qs(view, '#proxyPassword').value = cfg.ProxyPassword || '';
        setBool(view, 'enableLegacyGlobalProxyHook', cfg.EnableLegacyGlobalProxyHook);
        qs(view, '#tmdbApiKey').value = cfg.TmdbApiKey || '';
        qs(view, '#tmdbLanguage').value = cfg.TmdbLanguage || 'zh-CN';
        qs(view, '#tmdbRegion').value = cfg.TmdbRegion || 'CN';
        setBool(view, 'enableAdultMetadata', cfg.EnableAdultMetadata);
        setBool(view, 'enableTvdbFallback', cfg.EnableTvdbFallback);
        qs(view, '#tvdbApiKey').value = cfg.TvdbApiKey || '';
        qs(view, '#tvdbPin').value = cfg.TvdbPin || '';
        qs(view, '#tvdbLanguage').value = cfg.TvdbLanguage || 'zho';
        setBool(view, 'autoScanEnabled', cfg.AutoScanEnabled !== false);
        setBool(view, 'autoMetadataRefresh', cfg.AutoMetadataRefresh !== false);
        qs(view, '#scanIntervalMinutes').value = cfg.ScanIntervalMinutes || 10;
        qs(view, '#maxScrapeRetryCount').value = cfg.MaxScrapeRetryCount || 3;
    }

    function renderLibraries(view, libraries) {
        var list = qs(view, '#libraryList');
        list.innerHTML = '';

        if (!libraries || !libraries.length) {
            list.innerHTML = '<div class="fieldDescription">未获取到媒体库。</div>';
            return;
        }

        libraries.forEach(function (lib, index) {
            var wrapper = document.createElement('div');
            wrapper.className = 'checkboxContainer';
            wrapper.innerHTML = '' +
                '<input type="checkbox" class="lib-enabled" data-index="' + index + '" ' + (lib.Enabled !== false ? 'checked' : '') + ' />' +
                '<div class="checkboxText">' +
                '<div><strong>' + (lib.Name || '未命名媒体库') + '</strong></div>' +
                '<div class="fieldDescription">' + (lib.Path || lib.Id || '') + '</div>' +
                '</div>';
            list.appendChild(wrapper);
        });
    }

    function collectLibraries(view, existing) {
        var result = (existing || []).map(function (x) {
            return {
                Id: x.Id || '',
                Name: x.Name || '',
                Path: x.Path || '',
                Enabled: x.Enabled !== false
            };
        });

        var checks = view.querySelectorAll('.lib-enabled');
        for (var i = 0; i < checks.length; i++) {
            var idx = parseInt(checks[i].getAttribute('data-index'), 10);
            if (!isNaN(idx) && result[idx]) {
                result[idx].Enabled = checks[i].checked;
            }
        }

        return result;
    }

    function getPayload(view) {
        var config = view.__currentConfig || {};
        var libraries = collectLibraries(view, view.__libraries || config.Libraries || []);

        return {
            ProxyEnabled: getBool(view, 'proxyEnabled'),
            ProxyHost: qs(view, '#proxyHost').value.trim(),
            ProxyPort: intValue(view, 'proxyPort', 7890),
            ProxyUsername: qs(view, '#proxyUsername').value || '',
            ProxyPassword: qs(view, '#proxyPassword').value || '',
            EnableLegacyGlobalProxyHook: getBool(view, 'enableLegacyGlobalProxyHook'),
            TmdbApiKey: qs(view, '#tmdbApiKey').value.trim(),
            TmdbLanguage: qs(view, '#tmdbLanguage').value.trim() || 'zh-CN',
            TmdbRegion: qs(view, '#tmdbRegion').value.trim() || 'CN',
            EnableAdultMetadata: getBool(view, 'enableAdultMetadata'),
            EnableTvdbFallback: getBool(view, 'enableTvdbFallback'),
            TvdbApiKey: qs(view, '#tvdbApiKey').value.trim(),
            TvdbPin: qs(view, '#tvdbPin').value.trim(),
            TvdbLanguage: qs(view, '#tvdbLanguage').value.trim() || 'zho',
            AutoScanEnabled: getBool(view, 'autoScanEnabled'),
            ScanIntervalMinutes: intValue(view, 'scanIntervalMinutes', 10),
            AutoMetadataRefresh: getBool(view, 'autoMetadataRefresh'),
            MaxScrapeRetryCount: intValue(view, 'maxScrapeRetryCount', 3),
            Libraries: libraries
        };
    }

    function loadLibraries(view) {
        return api('/EmbyTMDBScraperFix/Libraries', 'GET').then(function (libs) {
            view.__libraries = libs || [];
            renderLibraries(view, view.__libraries);
        });
    }

    function loadPageData(view) {
        setStatus(view, '正在加载配置...');
        loading.show();

        return ApiClient.getPluginConfiguration(pluginId).then(function (config) {
            view.__currentConfig = config || {};
            fillForm(view, view.__currentConfig);
            return loadLibraries(view);
        }).then(function () {
            setStatus(view, '配置已加载。');
            loading.hide();
        }).catch(function (err) {
            loading.hide();
            setStatus(view, '加载失败：' + (err && err.message ? err.message : err), true);
        });
    }

    function bindEvents(view) {
        qs(view, '#EmbyTMDBScraperFixConfigForm').addEventListener('submit', function (e) {
            e.preventDefault();
            var payload = getPayload(view);
            setStatus(view, '正在保存配置...');
            loading.show();

            ApiClient.updatePluginConfiguration(pluginId, payload).then(function () {
                return ApiClient.getPluginConfiguration(pluginId);
            }).then(function (config) {
                view.__currentConfig = config || payload;
                fillForm(view, view.__currentConfig);
                Dashboard.processPluginConfigurationUpdateResult();
                setStatus(view, '配置已保存。');
                loading.hide();
            }).catch(function (err) {
                loading.hide();
                setStatus(view, '保存失败：' + (err && err.message ? err.message : err), true);
            });
        });

        qs(view, '#btnReload').addEventListener('click', function () {
            loadPageData(view);
        });

        qs(view, '#btnTestProxy').addEventListener('click', function () {
            setStatus(view, '正在测试代理...');
            loading.show();
            api('/EmbyTMDBScraperFix/TestProxy', 'POST', {}).then(function (result) {
                var items = (result && result.Items) || [];
                var lines = items.map(function (x) {
                    return (x.Success ? '成功' : '失败') + '：' + (x.Url || '') + ' ' + (x.Message || '');
                });
                setStatus(view, lines.join('\n') || '测试完成。', !result || result.Success === false);
                loading.hide();
            }).catch(function (err) {
                loading.hide();
                setStatus(view, '测试失败：' + (err && err.message ? err.message : err), true);
            });
        });

        qs(view, '#btnLogs').addEventListener('click', function () {
            setStatus(view, '正在读取日志...');
            loading.show();
            api('/EmbyTMDBScraperFix/Logs?Limit=20', 'GET').then(function (logs) {
                var lines = (logs || []).map(function (x) {
                    return (x.Time || '') + ' [' + (x.Level || '') + '] ' + (x.Message || '');
                });
                setStatus(view, lines.join('\n') || '暂无日志。');
                loading.hide();
            }).catch(function (err) {
                loading.hide();
                setStatus(view, '日志读取失败：' + (err && err.message ? err.message : err), true);
            });
        });
    }

    return function (view) {
        view.addEventListener('viewshow', function () {
            loadPageData(view);
        });

        bindEvents(view);
    };
});
