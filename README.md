# EmbyTMDBScraperFix

EmbyTMDBScraperFix is an open-source Emby Server plugin for mainland China network environments.

## Goals

- Provide an Emby-selectable custom metadata scraper provider
- Force TMDB overseas metadata requests through an HTTP proxy
- Keep local LAN / file system / intranet access untouched
- Run lightweight incremental scans every 10 minutes by default
- Re-trigger metadata refresh when media changes are detected

## Features

- HTTP proxy only
- Proxy connectivity test API
- TMDB-backed custom scraper provider
- Movie / Series / Episode metadata support
- Poster / backdrop image support
- Incremental media scan with retry
- Persistent plugin configuration
- Detailed plugin logs
- Optional high-risk global proxy hook

## Important Notes

This plugin follows the official Emby plugin model as closely as possible. The recommended path is to use it as a selectable metadata provider inside Emby library settings.

The optional high-risk global proxy hook exists for edge cases, but it cannot guarantee interception of every built-in Emby request.

## Build Requirements

- Emby Server 4.9.x developer environment
- .NET SDK with `netstandard2.0` build support
- Emby server reference assemblies

## Repository Layout

- `EmbyTMDBScraperFix.csproj`
- `Plugin.cs`
- `PluginRuntime.cs`
- `Configuration/PluginConfiguration.cs`
- `Controllers/ConfigurationService.cs`
- `Services/`
- `Tasks/`

## Installation

1. Build the project.
2. Copy `EmbyTMDBScraperFix.dll` into Emby Server plugins directory.
3. Restart Emby Server.
4. Configure proxy and TMDB API key through the plugin API or future UI page.
5. In Emby library settings, select `EmbyTMDBScraperFix TMDB Movie` / `EmbyTMDBScraperFix TMDB Series` as metadata providers.

## HTTP Proxy Configuration

1. Enable proxy.
2. Fill in proxy host and port.
3. Optionally fill username and password.
4. Test proxy connectivity via `/EmbyTMDBScraperFix/TestProxy`.

## Usage

1. Set TMDB API key.
2. Configure HTTP proxy if needed.
3. Add or edit libraries and choose EmbyTMDBScraperFix as the metadata provider.
4. Wait for scheduled incremental scans or trigger metadata refresh manually.

## API Endpoints

- `GET /EmbyTMDBScraperFix/Configuration`
- `POST /EmbyTMDBScraperFix/Configuration`
- `POST /EmbyTMDBScraperFix/TestProxy`
- `GET /EmbyTMDBScraperFix/Logs`

## FAQ

### Does it support Socks5?
No. HTTP proxy only.

### Does it affect local LAN access?
No, local and private network traffic is excluded from proxy rules.

### Will it intercept all built-in Emby scraper traffic?
Not reliably. Use this plugin as the selectable metadata provider. The optional global hook is best-effort only.

### Why do I need a TMDB API key?
This plugin uses TMDB as the primary metadata source.

## License

MIT
