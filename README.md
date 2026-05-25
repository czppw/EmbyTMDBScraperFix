# EmbyTMDBScraperFix

[![Build](https://github.com/czppw/EmbyTMDBScraperFix/actions/workflows/build.yml/badge.svg)](https://github.com/czppw/EmbyTMDBScraperFix/actions/workflows/build.yml)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

面向中国大陆网络环境的 Emby Server 插件，提供可代理的 TMDB 元数据抓取、中文搜索增强、`.strm` 支持、自动增量扫描、入库加速和缺集诊断能力。

## 当前公开版本

- `v1.3.4`
- 适配验证：`Emby 4.9.5.0`
- 实测环境：`Docker / Linux`

## v1.3.4 新增

- 新增入库加速开关和“同时刷新条目数”配置。
- 新增刷新队列状态接口。
- 新增刷新计划预览接口。
- 新增最近更新剧集刷新任务。
- 新增缺集诊断接口。
- 新增配置页缺集诊断面板。
- 新增按剧集名称查找并选择剧集的缺集诊断流程。
- 新增剧集候选列表诊断接口。
- 新增剧集层级诊断接口。

## v1.3.4 修复

- 修复配置页缓存导致新设置项不显示的问题。
- 修复缺集诊断需要手动填写 InternalId 或媒体路径的问题。
- 修复 Season 缺少 `IndexNumber` 时缺集诊断无法匹配季号的问题。
- 修复重复 Season 脏数据导致缺集诊断返回 `SeasonCount=0` 的问题。
- 修复自动增量刷新在 Emby 尚未索引为具体媒体条目时过早刷新父级的问题。

## 安装

1. 打开 [Releases](https://github.com/czppw/EmbyTMDBScraperFix/releases)
2. 下载最新版本的 `EmbyTMDBScraperFix.dll`
3. 复制到 Emby 插件目录
4. 重启 Emby Server

Docker / Linux 示例：

```bash
cp EmbyTMDBScraperFix.dll /path/to/emby/config/plugins/
docker restart emby
```

## 推荐库配置

电影库：

- `Metadata Fetchers`: `EmbyTMDBScraperFix TMDB Movie`
- `Image Fetchers`: 启用插件图片提供器

剧集库：

- `Metadata Fetchers`: `EmbyTMDBScraperFix TMDB Series`
- `Image Fetchers`: 启用插件图片提供器

## 推荐命名

电影：

```text
方世玉 (1993).strm
```

剧集：

```text
/TV/请回答1988 (2015)/Season 01/请回答1988 (2015) - S01E01.strm
```

## 常用诊断接口

- `GET /EmbyTMDBScraperFix/Diagnostics/ResolvePath`
- `GET /EmbyTMDBScraperFix/Diagnostics/ListIndexedItems`
- `GET /EmbyTMDBScraperFix/Diagnostics/ResolveInternalId`
- `GET /EmbyTMDBScraperFix/Diagnostics/RemoteImages`
- `GET /EmbyTMDBScraperFix/Diagnostics/RefreshQueue`
- `GET /EmbyTMDBScraperFix/Diagnostics/MissingEpisodes`
- `GET /EmbyTMDBScraperFix/Diagnostics/SeriesCandidates`
- `GET /EmbyTMDBScraperFix/Diagnostics/SeriesTree`
- `POST /EmbyTMDBScraperFix/Diagnostics/PreviewSearchVariants`
- `POST /EmbyTMDBScraperFix/Diagnostics/PreviewRefreshPlan`
- `POST /EmbyTMDBScraperFix/Diagnostics/RunRecentSeriesRefresh`
- `POST /EmbyTMDBScraperFix/Diagnostics/RefreshItem`
- `POST /EmbyTMDBScraperFix/Diagnostics/FillEpisodeNumbersFromPath`
- `POST /EmbyTMDBScraperFix/Diagnostics/RepairEpisodeNumbers`
- `POST /EmbyTMDBScraperFix/Diagnostics/RepairPersonMetadata`

## 文档

- [Wiki Home](wiki/Home.md)
- [安装指南](wiki/Installation.md)
- [配置说明](wiki/Configuration.md)
- [使用说明](wiki/Usage.md)
- [更新日志](wiki/Changelog.md)

## 许可证

MIT License
