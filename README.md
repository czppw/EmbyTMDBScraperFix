# EmbyTMDBScraperFix

适用于中国大陆网络环境的 Emby Server 插件，提供**可通过 HTTP 代理的 TMDB 自定义刮削器**和**轻量增量自动扫描**。

[![Build](https://github.com/czppw/EmbyTMDBScraperFix/actions/workflows/build.yml/badge.svg)](https://github.com/czppw/EmbyTMDBScraperFix/actions/workflows/build.yml)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

---

## 解决的问题

- **Emby 无法刮削**：国内网络无法直连 TMDB、TheTVDB 等海外元数据站点
- **媒体入库延迟**：Emby 默认库扫描间隔较长，新增媒体不能及时显示
- **插件只代理刮削流量**：不影响本地局域网、内网接口、文件系统等内部访问

## 功能特性

- ✅ **自定义 TMDB 刮削器** — 在媒体库设置中可选择本插件作为元数据提供器
- ✅ **HTTP 代理** — 仅对 TMDB 等海外域名走代理，本地流量不受影响
- ✅ **代理连通性测试** — 一键测试代理是否可用
- ✅ **支持电影/剧集/集** — 完整元数据+海报+背景图
- ✅ **增量自动扫描** — 默认 10 分钟扫描一次，仅检测新增/修改/删除文件
- ✅ **扫描后自动刷新元数据** — 检测到媒体变化时自动触发 TMDB 刮削
- ✅ **刮削失败自动重试** — 网络波动后下一轮扫描自动重试
- ✅ **配置持久化** — 重启 Emby 不丢失
- ✅ **完整日志系统** — 记录代理状态、扫描时间、文件变动、刮削结果
- ✅ **嵌入式插件配置页** — 在 Emby Dashboard 中可直接操作
- ✅ **可选高风险全局代理 Hook** — 极端情况下尝试劫持 Emby 内置请求

## 快速开始

### 安装

1. 从 [Releases](https://github.com/czppw/EmbyTMDBScraperFix/releases) 下载最新的 `EmbyTMDBScraperFix.dll`
2. 复制到 Emby Server 的 `plugins` 目录
3. 重启 Emby Server
4. 打开 Emby Dashboard → 插件 → 找到 **EmbyTMDBScraperFix** 进行配置

### 配置步骤

1. **填写 TMDB API Key** — 必须，插件使用 TMDB 作为元数据来源
2. **配置 HTTP 代理（可选）** — 如果网络环境需要代理才能访问 TMDB，请开启并填写代理地址和端口
3. **测试代理连通性** — 确认代理正常工作
4. **设置增量扫描间隔** — 默认 10 分钟，可根据需要调整
5. **选择参与扫描的媒体库** — 勾选需要自动扫描的库

### 在媒体库中使用本插件

1. 进入 Emby Dashboard → 媒体库
2. 添加新库或编辑已有库
3. 在**元数据提供器**列表中，勾选 `EmbyTMDBScraperFix TMDB Movie`（电影库）或 `EmbyTMDBScraperFix TMDB Series`（剧集库）
4. 将其拖动到优先级更高的位置（越靠前越优先使用）
5. 保存后即可开始刮削

## API 端点

插件注册了以下 REST API 端点，配置页会调用这些接口：

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/EmbyTMDBScraperFix/Configuration` | 获取当前配置 |
| POST | `/EmbyTMDBScraperFix/Configuration` | 更新配置 |
| POST | `/EmbyTMDBScraperFix/TestProxy` | 测试代理连通性 |
| GET | `/EmbyTMDBScraperFix/Logs` | 获取最近日志 |
| GET | `/EmbyTMDBScraperFix/Libraries` | 获取 Emby 虚拟媒体库列表 |

## 构建

### 环境要求

- .NET SDK 7.0+
- Emby Server 4.9.x

### 本地构建

```bash
git clone https://github.com/czppw/EmbyTMDBScraperFix.git
cd EmbyTMDBScraperFix
dotnet restore
dotnet build --configuration Release
```

编译产物位于 `bin/Release/netstandard2.0/EmbyTMDBScraperFix.dll`

### GitHub Actions 自动构建

推送代码到 `master` 分支后，GitHub Actions 会自动执行构建并上传 DLL 产物。

## 项目结构

```
EmbyTMDBScraperFix/
├── .github/workflows/build.yml              # CI 自动构建
├── Configuration/
│   ├── PluginConfiguration.cs                # 配置模型
│   └── EmbyTMDBScraperFixConfigurationPage.cs # 配置页注册
├── Controllers/
│   └── ConfigurationService.cs               # REST API 服务
├── Services/
│   ├── TmdbApiClient.cs                      # TMDB API 客户端
│   ├── TmdbMetadataProviders.cs              # 自定义刮削器提供器
│   ├── ProxyHttpClientService.cs             # 代理 HTTP 客户端
│   ├── ProxyPolicyService.cs                 # 代理策略（隔离本地流量）
│   ├── IncrementalScanService.cs             # 增量扫描服务
│   └── PluginLogService.cs                   # 日志服务
├── Tasks/
│   └── AutoIncrementalScanTask.cs            # Emby 定时任务
├── Web/
│   └── configuration.html                    # 插件配置页 UI
├── Plugin.cs                                 # 插件入口
├── PluginRuntime.cs                          # 运行时入口
├── EmbyTMDBScraperFix.csproj
├── README.md
└── LICENSE
```

## 常见问题

### HTTPS 代理支持吗？
不支持。仅支持 **HTTP 代理**。

### 会不会影响局域网访问？
不会。代理策略会**自动跳过**本地回环地址和私有 IP 地址段（10.x.x.x、172.16-31.x.x、192.168.x.x、169.254.x.x）。

### 能劫持 Emby 内置刮削器的全部流量吗？
**不能可靠做到。** 插件以**可选刮削器**的形式工作，需要在媒体库设置中手动选择。内置的全局代理 Hook 是**高风险**选项，默认关闭，仅在极端场景下开启，且无法保证100%生效。

### 为什么需要 TMDB API Key？
本插件使用 TMDB 官方 API 作为元数据来源，需要 API Key。可以在 [TMDB 官网](https://www.themoviedb.org/settings/api) 免费申请。

### 如何从 GitHub Actions 下载编译好的 DLL？
1. 打开仓库的 Actions 页面
2. 点击最新的成功构建
3. 在页面底部找到 **Artifacts**，点击 `EmbyTMDBScraperFix` 下载

## 许可证

[MIT](LICENSE)
