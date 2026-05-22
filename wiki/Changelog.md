# 更新日志

## v1.0.2 (2026-05-22)
### 新功能
- **事件驱动增量扫描** — 改用 FileSystemWatcher 实时监听文件变更，替代周期全量扫描
- **持久化变更队列** — 未消费的变更事件写入 `pending-changes.json`，Emby 重启后仍可恢复消费
- **新增 .strm 文件支持** — watcher 现在能识别 .strm 文件的增删改

### 修复
- `.strm` 扩展名未加入 `MediaExtensions` 列表导致 watcher 忽略 .strm 文件变更

## v1.0.1 (2026-05-22)
### 修复
- 配置页修改扫描间隔后，Emby 计划任务显示未同步
- 注入 ITaskManager，配置保存时同步更新计划任务触发器
- 重写 `UpdateConfiguration()`，使默认配置保存 API 也触发配置生效

## v1.0.0 (2026-05-21)
### 功能
- TMDB 电影、电视剧、分季、剧集元数据刮削
- TVDB 元数据补充刮削（可选）
- HTTP 代理支持
- 自定义 TMDB API 基址
- 增量自动扫描
- Emby 配置页面
