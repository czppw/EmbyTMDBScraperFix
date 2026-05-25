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

1. 在插件配置页填写 `TMDB API Key`
2. 如需代理，配置 `HTTP Proxy`
3. 测试一个电影条目
4. 测试一个剧集条目
5. 测试一个 `.strm` 增量变更
6. 在缺集诊断中按剧名查找剧集并执行检查

## 缺集诊断

1. 打开插件配置页
2. 在 `剧集名称` 中输入剧名
3. 点击 `查找剧集`
4. 在 `选择剧集` 下拉框中选择目标剧集
5. 点击 `检查缺集`

## 常用修复接口

### 预览搜索变体

```text
POST /EmbyTMDBScraperFix/Diagnostics/PreviewSearchVariants
```

### 修复缺失分集编号

```text
POST /EmbyTMDBScraperFix/Diagnostics/FillEpisodeNumbersFromPath
POST /EmbyTMDBScraperFix/Diagnostics/RepairEpisodeNumbers
```

### 修复人物元数据

```text
POST /EmbyTMDBScraperFix/Diagnostics/RepairPersonMetadata
```

### 查看远程图片

```text
GET /EmbyTMDBScraperFix/Diagnostics/RemoteImages
```
