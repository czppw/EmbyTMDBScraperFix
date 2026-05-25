# 安装指南

## 系统要求

- Emby `4.9.x`
- 可挂载插件目录的部署方式
- 如需代理，当前支持 `HTTP Proxy`

## 下载

1. 打开 [Releases](https://github.com/czppw/EmbyTMDBScraperFix/releases)
2. 下载最新 `EmbyTMDBScraperFix.dll`

## 部署

将 DLL 复制到 Emby 插件目录后重启 Emby。

Docker / Linux 示例：

```bash
cp EmbyTMDBScraperFix.dll /path/to/emby/config/plugins/
docker restart emby
```

## 升级建议

1. 替换旧 DLL
2. 重启 Emby
3. 对目标库执行一次刷新
4. 如旧库存在异常条目，优先清理重复库和历史测试库
