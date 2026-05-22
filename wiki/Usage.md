# 使用说明

## 在媒体库中使用
创建或编辑媒体库时，在元数据下载器中勾选：
- **电影**: `EmbyTMDBScraperFix TMDB Movie`
- **电视剧**: `EmbyTMDBScraperFix TMDB Series`

建议取消勾选其他 TMDB 刮削器避免冲突。

## 自动增量扫描
插件独立于 Emby 自带的扫描任务运行：
1. 定时扫描媒体目录
2. 检测文件新增/删除/修改
3. 自动通知 Emby 入库/出库
4. 可选触发元数据刮削

配置页修改间隔后会同步更新 Emby 计划任务显示。
