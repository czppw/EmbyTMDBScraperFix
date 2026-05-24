# 安装指南

## 系统要求

- Emby `4.9.x`
- 支持插件目录挂载的部署方式
- 如需代理，仅支持 `HTTP Proxy`

## 下载安装

### Releases

1. 打开 [Releases](https://github.com/czppw/EmbyTMDBScraperFix/releases)
2. 下载最新 `EmbyTMDBScraperFix.dll`

### Actions 产物

1. 打开 [Build Workflow](https://github.com/czppw/EmbyTMDBScraperFix/actions/workflows/build.yml)
2. 进入最近一次成功构建
3. 下载产物 `EmbyTMDBScraperFix`
4. 解压得到 `EmbyTMDBScraperFix.dll`

## 部署

将 DLL 复制到 Emby 插件目录后重启 Emby。

Docker / Linux 常见示例：

```bash
cp EmbyTMDBScraperFix.dll /path/to/emby/config/plugins/
docker restart emby
```

## 升级

从旧版本升级时建议：

1. 备份 Emby 配置目录
2. 替换旧 DLL
3. 重启 Emby
4. 对目标库执行一次刷新
5. 如旧库存在异常条目，优先清理重复库和历史测试库
