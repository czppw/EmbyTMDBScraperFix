# 使用说明

## 在媒体库中使用 EmbyTMDBScraperFix

### 创建媒体库时
在 Emby 控制台 → 媒体库 → 新增媒体库：
1. 选择内容类型（电影 / 电视剧）
2. 选择媒体文件夹
3. 在 **元数据下载器** 中勾选 `EmbyTMDBScraperFix TMDB Movie`（电影）或 `EmbyTMDBScraperFix TMDB Series`（电视剧）
4. 建议取消勾选其他 TMDB/TheMovieDb 刮削器以避免冲突

### 已有媒体库
Emby 控制台 → 媒体库 → 编辑 → 元数据下载器 → 添加 `EmbyTMDBScraperFix` 系列刮削器

### .strm 文件支持
插件完整支持 `.strm` 文件的增删改监控与元数据刮削：
- 新增 `.strm` → 自动检测并入库
- 修改 `.strm` → 触发元数据刷新
- 删除 `.strm` → 自动出库

### 自动增量扫描
插件内置事件驱动增量扫描，工作原理：
1. 使用 `FileSystemWatcher` 实时监听媒体目录
2. 增删改事件写入持久化队列（`pending-changes.json`）
3. 定时消费队列，将变更通知 Emby
4. **Emby 重启后** 未消费的队列事件仍可恢复

> ⚠️ 注意：插件内部使用独立定时器运行扫描，与 Emby 自带的"扫描媒体库"计划任务互相独立。插件配置页修改扫描间隔后会同步更新 Emby 计划任务显示。
