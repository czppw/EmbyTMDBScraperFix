# 使用说明

## 推荐目录结构

电影：

```text
/Movies/方世玉 (1993).strm
```

剧集：

```text
/TV/请回答1988 (2015)/Season 01/请回答1988 (2015) - S01E01.strm
```

## 最小验证流程

### 1. 代理验证

在插件配置页填写代理和 TMDB API Key 后点击“测试代理”。

验证点：

- `api.tmdb.org` 可访问
- 图片域名可访问
- 日志中不明文泄露 API Key

### 2. 电影验证

选择一个电影，刷新元数据并确认：

- 名称
- 简介
- TMDB ID
- 人物
- 海报

### 3. 剧集验证

选择一个剧集，确认：

- `Series -> Season -> Episode` 层级正常
- 所有分集都有 `Tmdb ID`
- `IndexNumber` 和 `ParentIndexNumber` 正常
- 分集中文名称正常

### 4. 分集图片验证

确认日志中出现：

- `GetImages start`
- `GetImages finished`

并在 Emby 中确认图片已写入 `ImageTags`。

### 5. `.strm` 增量验证

新增或修改一个 `.strm` 文件后，确认日志出现增量扫描记录。

## 历史异常条目修复

### 修复缺失分集编号

单集：

```text
POST /EmbyTMDBScraperFix/Diagnostics/FillEpisodeNumbersFromPath
```

批量：

```text
POST /EmbyTMDBScraperFix/Diagnostics/RepairEpisodeNumbers
```

### 查看远程图片提供器

```text
GET /EmbyTMDBScraperFix/Diagnostics/RemoteImages
```

### 强制刷新条目

```text
POST /EmbyTMDBScraperFix/Diagnostics/RefreshItem
```
