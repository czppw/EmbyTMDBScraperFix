# EmbyTMDBScraperFix

[![Build](https://github.com/czppw/EmbyTMDBScraperFix/actions/workflows/build.yml/badge.svg)](https://github.com/czppw/EmbyTMDBScraperFix/actions/workflows/build.yml)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

面向中国大陆网络环境的 Emby Server 插件，提供可代理的 TMDB/TVDB 元数据抓取、增量扫描、`.strm` 支持，以及电影、剧集、分季、分集、人物和图片刮削能力。

## 当前版本

- `v1.1.0`
- 适配验证：`Emby 4.9.5.0`
- 实测环境：`Docker / Linux`

## v1.1.0 重点更新

- 修复 Emby 插件页面版本号显示为 `0.0.0.0`
- 新增插件封面缩略图，插件列表不再只显示默认文件夹图标
- 继承 v1.0.2 的全部事件驱动扫描和持久化队列、图片、人物和增量刷新修复
- 修复电影、剧集、分季、分集、人物的 TMDB/TVDB 元数据链路
- 修复人物写入 Emby `People` 列表
- 修复远程图片被语言过滤导致的海报不下载问题
- 新增分集图片支持
- 修复 `.strm` 增量刷新过早重试的问题
- 新增更稳健的 TMDB 搜索回退策略
- 新增批量修复分集编号、路径/条目/图片诊断接口
- 修复前端 `innerHTML` 风险和若干兼容性问题

## 已验证结果

- 电影：名称、简介、海报、类型、人物正常
- 剧集：`Series -> Season -> Episode` 层级正常
- 分集：`S01E01 ~ S01E20` 可正确写入 `Tmdb ID / IndexNumber / ParentIndexNumber`
- 人物：可写入演员信息和人物简介
- 图片：电影海报、剧集海报、分集图片链路正常
- 增量：`.strm` 文件变更可进入延后刷新流程

## 安装

### 方式一：从 Releases 下载

1. 打开 [Releases](https://github.com/czppw/EmbyTMDBScraperFix/releases)
2. 下载最新版本的 `EmbyTMDBScraperFix.dll`
3. 复制到 Emby 插件目录
4. 重启 Emby Server

### Docker / Linux 部署

常见插件目录示例：

```text
/config/plugins/
```

复制后重启容器：

```bash
docker restart emby
```

## Emby 库配置建议

电影库：

- `Metadata Fetchers` 启用 `EmbyTMDBScraperFix TMDB Movie`
- `Image Fetchers` 启用插件图片提供器

剧集库：

- `Metadata Fetchers` 启用 `EmbyTMDBScraperFix TMDB Series`
- `Image Fetchers` 启用插件图片提供器

建议在测试阶段临时关闭同类内置抓取器，先确认插件链路单独正常，再按需要恢复。

## 推荐命名

电影：

```text
方世玉 (1993).strm
```

剧集：

```text
/TV-Test/请回答1988 (2015)/Season 01/请回答1988 (2015) - S01E01.strm
```

## 测试流程

1. 在插件配置页填写 `TMDB API Key`
2. 如需代理，配置 `HTTP Proxy` 并点击“测试代理”
3. 新建最小测试库
4. 先测试一个电影条目：
   - 刷新元数据
   - 确认名称、简介、人物、海报
5. 再测试一个剧集条目：
   - 确认 `Series / Season / Episode` 层级
   - 确认每集 `TMDB ID / IndexNumber / ParentIndexNumber`
6. 测试一个 `.strm` 文件增量变更：
   - 新增或修改文件
   - 观察插件日志

## 升级注意事项

- 从旧版本升级到 `v1.1.0` 后，建议对受影响的库执行一次手动刷新
- 对于历史脏条目，优先使用标准目录结构重新扫描
- 如果旧库中存在分集编号缺失，可用诊断接口批量修复
- 如果旧电影已经有空图片状态，执行一次图片刷新即可补图，不需要重建整个库
- 如果存在重复库、重叠路径或旧测试库，建议先清理，避免 Emby 选错库配置

## 诊断接口

插件当前提供以下常用诊断接口：

- `GET /EmbyTMDBScraperFix/Diagnostics/ResolvePath`
- `GET /EmbyTMDBScraperFix/Diagnostics/ListIndexedItems`
- `GET /EmbyTMDBScraperFix/Diagnostics/ResolveInternalId`
- `GET /EmbyTMDBScraperFix/Diagnostics/RemoteImages`
- `POST /EmbyTMDBScraperFix/Diagnostics/RefreshItem`
- `POST /EmbyTMDBScraperFix/Diagnostics/FillEpisodeNumbersFromPath`
- `POST /EmbyTMDBScraperFix/Diagnostics/RepairEpisodeNumbers`

## 源码构建

```bash
git clone https://github.com/czppw/EmbyTMDBScraperFix.git
cd EmbyTMDBScraperFix
dotnet restore
dotnet build --configuration Release
```

输出文件：

```text
bin/Release/netstandard2.0/EmbyTMDBScraperFix.dll
```

## 文档

- [Wiki Home](wiki/Home.md)
- [安装指南](wiki/Installation.md)
- [配置说明](wiki/Configuration.md)
- [使用说明](wiki/Usage.md)
- [更新日志](wiki/Changelog.md)

## 许可证

MIT License
