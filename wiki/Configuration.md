# 配置说明

## TMDB

- `TMDB API Key`：必填
- `TMDB Language`：建议 `zh-CN`
- `TMDB Region`：建议按实际地区设置
- `EnableAdultMetadata`：按需开启

## TVDB 回退

- `EnableTvdbFallback`
- `TvdbApiKey`
- `TvdbPin`
- `TvdbLanguage`

仅当 TMDB 数据不完整或希望多源回退时开启。

## 代理

- `ProxyEnabled`
- `ProxyHost`
- `ProxyPort`
- `ProxyUsername`
- `ProxyPassword`
- `EnableLegacyGlobalProxyHook`

说明：

- 当前主要支持 `HTTP Proxy`
- 推荐优先使用插件内部代理，不建议默认开启高风险全局 Hook

## 自动扫描

- `AutoScanEnabled`
- `ScanIntervalMinutes`
- `AutoMetadataRefresh`
- `MaxScrapeRetryCount`
- `Libraries`

建议：

- 扫描间隔不低于 `1` 分钟
- `.strm` 环境建议开启自动元数据刷新
- 仅勾选需要监控的媒体库

## 库内抓取器建议

电影：

- `Metadata Fetchers`: `EmbyTMDBScraperFix TMDB Movie`
- `Image Fetchers`: 插件图片提供器

剧集：

- `Metadata Fetchers`: `EmbyTMDBScraperFix TMDB Series`
- `Image Fetchers`: 插件图片提供器
