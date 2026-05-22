# EmbyTMDBScraperFix

Emby TMDB 刮削修复插件，专为中国大陆网络环境优化。

## 特性
- 🔄 **TMDB 请求代理** — 支持 HTTP/SOCKS5 代理访问 TMDB API
- 🏠 **自定义 API 基址** — 支持配置 TMDB 镜像站地址
- ⏱ **增量自动扫描** — 定时检测媒体库变更，可自定义扫描间隔
- 📺 **双数据源** — TMDB 为主，TVDB 为可选补充
- 🎬 **全面支持** — 电影、电视剧、分季、剧集元数据刮削

## 快速开始
1. 从 [Releases](https://github.com/czppw/EmbyTMDBScraperFix/releases) 下载 `EmbyTMDBScraperFix.dll`
2. 放入 Emby 的 `plugins` 目录
3. 重启 Emby Server
4. 进入 Emby 控制台 → 插件 → EmbyTMDBScraperFix → 配置
5. 填入 TMDB API Key 并保存
