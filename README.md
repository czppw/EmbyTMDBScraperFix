# EmbyTMDBScraperFix

适用于中国大陆网络环境的 Emby Server 插件，提供：

- **可通过 HTTP 代理访问的自定义刮削器**
- **TMDB 主源 + TheTVDB 备用源**
- **电影 / 剧集 / 分季 / 分集 / 人物（Person）元数据支持**
- **10 分钟默认增量自动扫描与自动刷新**

[![Build](https://github.com/czppw/EmbyTMDBScraperFix/actions/workflows/build.yml/badge.svg)](https://github.com/czppw/EmbyTMDBScraperFix/actions/workflows/build.yml)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

---

## 一、项目定位

`EmbyTMDBScraperFix` 是一个面向中国大陆用户的 Emby Server 插件，解决以下常见问题：

1. **Emby 无法正常刮削海外元数据**
   - 国内网络环境下，TMDB / TheTVDB 等站点常常无法直连
2. **媒体入库不及时**
   - 新增媒体后，要等待较久才能在 Emby 中看到更新
3. **不希望影响本地局域网和内网访问**
   - 只让“刮削流量”走 HTTP 代理，不影响本地文件、本地服务和内网 API

---

## 二、核心功能

### 1. HTTP 代理支持

- 仅支持 **HTTP 代理**
- 不支持 Socks5 / HTTPS 代理
- 只对海外元数据站点走代理，默认包含：
  - TMDB
  - TheTVDB
  - TVDB 图片域名
- TMDB API 地址支持配置替代域名：默认 `https://api.tmdb.org`；如果配置留空，则回落到系统默认 `https://api.themoviedb.org`
- 自动排除本地地址和私有网段：
  - `127.0.0.1`
  - `localhost`
  - `10.x.x.x`
  - `172.16.x.x - 172.31.x.x`
  - `192.168.x.x`
  - `169.254.x.x`

### 2. 自定义元数据提供器

插件当前已提供以下可选刮削器：

- `EmbyTMDBScraperFix TMDB Movie`
- `EmbyTMDBScraperFix TMDB Series`
- `EmbyTMDBScraperFix TMDB Season`
- `EmbyTMDBScraperFix TMDB Episode`
- `EmbyTMDBScraperFix TMDB Person`

可在 Emby 媒体库设置的**元数据提供器**列表里勾选并调整优先级。

### 3. TheTVDB 备用源

当 TMDB 数据缺失、或 TV 类元数据不完整时，可选启用 **TheTVDB fallback**。

当前 TheTVDB 备用支持覆盖：

- 剧集（Series）
- 分季（Season）
- 分集（Episode）
- 人物（Person）

> 注意：TheTVDB 使用时需要你提供 `API Key`，某些场景还可能需要 `PIN`。

### 4. 分季与人物刮削支持

除了电影 / 剧集 / 分集外，插件现在还支持：

#### Season（分季）
- 季名称
- 季简介
- 季图片
- 季编号
- 可从 TMDB 获取，必要时回退到 TheTVDB

#### Person（人物）
- 人物名称
- 人物简介
- 人物头像
- 出生信息（作为可映射年份来源）
- 可从 TMDB 获取，必要时回退到 TheTVDB

### 5. 增量自动扫描

- 默认 **10 分钟**扫描一次
- 周期自动扫描由插件内部后台定时器驱动，配置页保存后会立即按 `ScanIntervalMinutes` 生效
- Emby 计划任务页中的 `EmbyTMDBScraperFix Auto Incremental Scan` 仅保留为手动运行入口，不再作为周期调度真值来源
- 仅检测：
  - 新增文件
  - 修改文件
  - 删除文件
- 不做全盘全库无脑扫描
- 支持配置只扫描指定媒体库

### 6. 自动元数据刷新

扫描检测到文件变化后：

- 自动触发 Emby 元数据刷新
- 自动重新刮削图片和基础信息
- 刮削失败时会按配置进行重试

### 7. 日志系统

插件会记录：

- 代理状态
- 代理测试结果
- 扫描时间
- 文件变动
- 元数据刷新结果
- 错误信息

并提供日志接口供配置页查看。

---

## 三、当前支持范围

### 已支持

- 电影 Movie
- 剧集 Series
- 分季 Season
- 分集 Episode
- 人物 Person
- TheTVDB fallback
- 嵌入式配置页
- 自动扫描
- 自动刷新
- GitHub Actions 自动构建

### 当前实现说明

- **TMDB 是主源**
- **TheTVDB 是备用源**
- 插件是**可选刮削器**，需要在 Emby 媒体库中手动启用
- **不能保证无差别劫持 Emby 内置全部刮削流量**
- 高风险全局代理 Hook 仅做“尽力而为”的补充

---

## 四、安装方法

### 方式一：下载 GitHub Actions 构建产物

1. 打开仓库 Actions 页面
2. 选择最新成功的构建
3. 在页面底部下载 `Artifacts`
4. 得到 `EmbyTMDBScraperFix.dll`

### 方式二：本地编译

环境要求：

- .NET SDK 7.0+
- Emby Server 4.9.x

执行：

```bash
git clone https://github.com/czppw/EmbyTMDBScraperFix.git
cd EmbyTMDBScraperFix
dotnet restore
dotnet build --configuration Release
```

编译产物位置：

```text
bin/Release/netstandard2.0/EmbyTMDBScraperFix.dll
```

### 部署到 Emby

把 DLL 复制到 Emby 的插件目录，例如：

```text
<Emby数据目录>/plugins/
```

然后：

1. 重启 Emby Server
2. 打开 Dashboard → 插件
3. 找到 `EmbyTMDBScraperFix`
4. 进入配置页

---

## 五、配置说明

### 1. TMDB 设置

- `TMDB API Key`
- `TMDB 语言`
- `TMDB 区域`
- `是否启用成人元数据`

### 2. TheTVDB 设置

- `EnableTvdbFallback`
- `TvdbApiKey`
- `TvdbPin`
- `TvdbLanguage`

### 3. HTTP 代理设置

- `ProxyEnabled`
- `ProxyHost`
- `ProxyPort`
- `ProxyUsername`
- `ProxyPassword`
- `EnableLegacyGlobalProxyHook`（高风险）

### 4. 自动扫描设置

- `AutoScanEnabled`
- `ScanIntervalMinutes`
- `AutoMetadataRefresh`
- `MaxScrapeRetryCount`
- `Libraries`

> 注：周期自动扫描以插件配置页里的 `ScanIntervalMinutes` 为准，由插件内部后台定时器执行；计划任务页主要用于手动触发一次扫描。

---

## 六、如何在媒体库中使用本插件

1. 进入 Emby Dashboard → 媒体库
2. 新建媒体库或编辑现有媒体库
3. 在 **Metadata Providers / 元数据提供器** 中选择：
   - 电影库：`EmbyTMDBScraperFix TMDB Movie`
   - 剧集库：`EmbyTMDBScraperFix TMDB Series`
4. 将本插件拖到更高优先级
5. 保存设置

对于 Season / Episode / Person，Emby 会在对应对象刷新时使用插件提供器参与元数据获取。

---

## 七、API 端点

插件当前提供以下接口：

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/EmbyTMDBScraperFix/Configuration` | 获取当前配置 |
| POST | `/EmbyTMDBScraperFix/Configuration` | 保存配置 |
| POST | `/EmbyTMDBScraperFix/TestProxy` | 测试代理连通性 |
| GET | `/EmbyTMDBScraperFix/Logs` | 读取最近日志 |
| GET | `/EmbyTMDBScraperFix/Libraries` | 获取虚拟媒体库列表 |

---

## 八、项目结构

```text
EmbyTMDBScraperFix/
├── .github/workflows/build.yml
├── Configuration/
│   ├── PluginConfiguration.cs
│   └── EmbyTMDBScraperFixConfigurationPage.cs
├── Controllers/
│   └── ConfigurationService.cs
├── Services/
│   ├── TmdbApiClient.cs
│   ├── TvdbApiClient.cs
│   ├── TmdbMetadataProviders.cs
│   ├── ProxyHttpClientService.cs
│   ├── ProxyPolicyService.cs
│   ├── IncrementalScanService.cs
│   └── PluginLogService.cs
├── Tasks/
│   └── AutoIncrementalScanTask.cs
├── Web/
│   └── configuration.html
├── Plugin.cs
├── PluginRuntime.cs
├── EmbyTMDBScraperFix.csproj
├── README.md
└── LICENSE
```

---

## 九、常见问题

### 1. 支持 HTTPS 代理吗？
不支持，目前只支持 **HTTP 代理**。

### 2. 会不会影响本地局域网访问？
不会。插件默认只代理元数据站点，并排除本地/私有地址。

### 3. TheTVDB 一定需要配置吗？
不是必须。默认可以只使用 TMDB。只有在你希望剧集 / 分季 / 分集 / 人物有备用源时，才建议启用。

### 4. 为什么 Season / Person 不是单独勾选？
因为它们是 Emby 元数据体系的一部分。当你启用了对应主元数据提供器并触发刷新时，插件会在相应对象上参与工作。


---

## 十、许可证

本项目使用 [MIT License](LICENSE)。
