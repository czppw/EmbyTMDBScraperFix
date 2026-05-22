# 安装指南

## 手动安装
1. 前往 [Releases 页面](https://github.com/czppw/EmbyTMDBScraperFix/releases) 下载最新的 `EmbyTMDBScraperFix.dll`
2. 将 DLL 放入 Emby 插件目录
3. 重启 Emby Server
4. 在控制台 → 插件中确认已加载

## Docker 环境
```bash
cp EmbyTMDBScraperFix.dll /path/to/emby/config/plugins/
docker restart emby
```

## 源码编译
```bash
git clone https://github.com/czppw/EmbyTMDBScraperFix.git
cd EmbyTMDBScraperFix
dotnet build -c Release
# 产物：bin/Release/netstandard2.0/EmbyTMDBScraperFix.dll
```
