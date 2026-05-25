# EmbyTMDBScraperFix

[![Build](https://github.com/czppw/EmbyTMDBScraperFix/actions/workflows/build.yml/badge.svg)](https://github.com/czppw/EmbyTMDBScraperFix/actions/workflows/build.yml)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

面向中国大陆网络环境的 Emby Server 插件，提供可代理的 TMDB 元数据抓取、中文搜索增强、`.strm` 支持，以及电影、剧集、分季、分集、人物和图片刮削能力。

## 当前公开版本

- `v1.2.0`
- 适配验证：`Emby 4.9.5.0`
- 实测环境：`Docker / Linux`

## v1.2.0 新增功能

- 中文搜索增强配置项
- 标题规范化与搜索变体生成
- 简繁搜索回退
- 路径 / 文件名 / 文件夹名搜索回退
- 搜索变体预览诊断接口
- TMDB 图片地址配置项
- TMDB 回退语言配置项
- 搜索决策日志 `TMDB search selected` / `TMDB search rejected`
- Series 官方别名精确优先
- 人物元数据强制修复接口 `RepairPersonMetadata`

## v1.2.0 修复

- 补全中文搜索简繁映射缺失字符
- 清理 `264 / 265 / PCM / PCM_S24LE` 等技术标签残留
- 修复全角 `：` 规范化问题
- 修复代理域名列表重复与历史残留域名问题
- 移除旧备用源配置和相关残留逻辑
- 修复插件启动阶段的配置归一化崩溃

## 安装

1. 打开 [Releases](https://github.com/czppw/EmbyTMDBScraperFix/releases)
2. 下载最新版本的 `EmbyTMDBScraperFix.dll`
3. 复制到 Emby 插件目录
4. 重启 Emby Server

Docker / Linux 常见示例：

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
- `POST /EmbyTMDBScraperFix/Diagnostics/PreviewSearchVariants`
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
