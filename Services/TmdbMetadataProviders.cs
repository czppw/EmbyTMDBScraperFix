using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EmbyTMDBScraperFix.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace EmbyTMDBScraperFix.Services;

internal static class TmdbProviderFactory
{
    public static TmdbApiClient CreateClient(IApplicationPaths paths)
    {
        var log = new PluginLogService(paths);
        var policy = new ProxyPolicyService(log);
        var http = new ProxyHttpClientService(policy, log);
        return new TmdbApiClient(http, log);
    }

    public static Task<HttpResponseInfo> GetImageResponseAsync(string url, CancellationToken cancellationToken)
    {
        if (PluginRuntime.TryGetInstance(out var runtime))
        {
            return runtime.ProxyClient.GetResponseInfoAsync(url, Plugin.Instance?.Configuration ?? new PluginConfiguration(), cancellationToken);
        }

        throw new InvalidOperationException("EmbyTMDBScraperFix runtime is not initialized yet.");
    }

    public static RemoteSearchResult ToRemoteSearchResult(TmdbSearchItem item, string providerName, string? imageUrl, bool isSeries)
    {
        var date = isSeries ? item.First_Air_Date : item.Release_Date;
        var name = isSeries ? item.Name ?? item.Original_Name : item.Title ?? item.Original_Title;
        var result = new RemoteSearchResult
        {
            Name = name ?? string.Empty,
            OriginalTitle = isSeries ? item.Original_Name : item.Original_Title,
            Overview = item.Overview,
            ProductionYear = ParseYear(date),
            PremiereDate = ParseDate(date),
            ImageUrl = imageUrl,
            SearchProviderName = providerName
        };
        result.ProviderIds["Tmdb"] = item.Id.ToString(CultureInfo.InvariantCulture);
        return result;
    }

    public static int? ParseYear(string? date) => DateTime.TryParse(date, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt) ? dt.Year : null;
    public static DateTimeOffset? ParseDate(string? date) => DateTimeOffset.TryParse(date, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dt) ? dt : null;
}

public sealed class TmdbMovieMetadataProvider : IRemoteMetadataProvider<MovieInfo, Movie>, IRemoteImageProvider, IHasOrder, IHasSupportedExternalIdentifiers
{
    private readonly TmdbApiClient _tmdb;

    public TmdbMovieMetadataProvider(IApplicationPaths paths)
    {
        _tmdb = TmdbProviderFactory.CreateClient(paths);
    }

    public string Name => "EmbyTMDBScraperFix TMDB Movie";
    public int Order => 0;
    public string[] GetSupportedExternalIdentifiers() => new[] { "Tmdb" };
    public bool Supports(BaseItem item) => item is Movie;

    public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(MovieInfo info, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(info.Name)) return Array.Empty<RemoteSearchResult>();
        var cfg = Plugin.Instance?.Configuration ?? new PluginConfiguration();
        var search = await _tmdb.SearchMovieAsync(info.Name, info.Year, cfg, cancellationToken).ConfigureAwait(false);
        return search?.Results?.Select(x => TmdbProviderFactory.ToRemoteSearchResult(x, Name, _tmdb.GetImageUrl(x.Poster_Path, "w500"), false)).ToArray()
               ?? Array.Empty<RemoteSearchResult>();
    }

    public async Task<MetadataResult<Movie>> GetMetadata(MovieInfo info, CancellationToken cancellationToken)
    {
        var result = new MetadataResult<Movie>();
        var cfg = Plugin.Instance?.Configuration ?? new PluginConfiguration();
        var tmdbId = info.ProviderIds != null && info.ProviderIds.TryGetValue("Tmdb", out var id) ? id : null;
        TmdbTitle? data = !string.IsNullOrWhiteSpace(tmdbId)
            ? await _tmdb.GetMovieAsync(tmdbId, cfg, cancellationToken).ConfigureAwait(false)
            : null;

        if (data == null)
        {
            var search = await GetSearchResults(info, cancellationToken).ConfigureAwait(false);
            var match = search.FirstOrDefault();
            if (match == null || !match.ProviderIds.TryGetValue("Tmdb", out tmdbId)) return result;
            data = await _tmdb.GetMovieAsync(tmdbId, cfg, cancellationToken).ConfigureAwait(false);
        }

        if (data == null) return result;
        var item = new Movie
        {
            Name = data.Title ?? data.Original_Title ?? info.Name,
            Overview = data.Overview,
            ProductionYear = TmdbProviderFactory.ParseYear(data.Release_Date) ?? info.Year,
            CommunityRating = (float?)data.Vote_Average,
            PremiereDate = TmdbProviderFactory.ParseDate(data.Release_Date)
        };
        item.ProviderIds["Tmdb"] = data.Id.ToString(CultureInfo.InvariantCulture);
        foreach (var genre in data.Genres.Where(g => !string.IsNullOrWhiteSpace(g.Name))) item.AddGenre(genre.Name!);
        result.Item = item;
        result.HasMetadata = true;
        result.Provider = Name;
        result.ResultLanguage = cfg.TmdbLanguage;
        return result;
    }

    public IEnumerable<ImageType> GetSupportedImages(BaseItem item) => new[] { ImageType.Primary, ImageType.Backdrop };

    public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, LibraryOptions libraryOptions, CancellationToken cancellationToken)
    {
        var cfg = Plugin.Instance?.Configuration ?? new PluginConfiguration();
        var tmdbId = item.ProviderIds.TryGetValue("Tmdb", out var id) ? id : null;
        if (string.IsNullOrWhiteSpace(tmdbId)) return Array.Empty<RemoteImageInfo>();
        var data = await _tmdb.GetMovieAsync(tmdbId, cfg, cancellationToken).ConfigureAwait(false);
        return BuildImages(data, cfg, Name, _tmdb);
    }

    public Task<HttpResponseInfo> GetImageResponse(string url, CancellationToken cancellationToken) => TmdbProviderFactory.GetImageResponseAsync(url, cancellationToken);

    private static IEnumerable<RemoteImageInfo> BuildImages(TmdbTitle? data, PluginConfiguration cfg, string name, TmdbApiClient tmdb)
    {
        if (data == null) return Array.Empty<RemoteImageInfo>();
        var images = new List<RemoteImageInfo>();
        if (!string.IsNullOrWhiteSpace(data.Poster_Path)) images.Add(new RemoteImageInfo { ProviderName = name, Url = tmdb.GetImageUrl(data.Poster_Path), ThumbnailUrl = tmdb.GetImageUrl(data.Poster_Path, "w500"), Type = ImageType.Primary, Language = cfg.TmdbLanguage });
        if (!string.IsNullOrWhiteSpace(data.Backdrop_Path)) images.Add(new RemoteImageInfo { ProviderName = name, Url = tmdb.GetImageUrl(data.Backdrop_Path), ThumbnailUrl = tmdb.GetImageUrl(data.Backdrop_Path, "w780"), Type = ImageType.Backdrop, Language = cfg.TmdbLanguage });
        return images;
    }
}

public sealed class TmdbSeriesMetadataProvider : IRemoteMetadataProvider<SeriesInfo, Series>, IRemoteImageProvider, IHasOrder, IHasSupportedExternalIdentifiers
{
    private readonly TmdbApiClient _tmdb;

    public TmdbSeriesMetadataProvider(IApplicationPaths paths)
    {
        _tmdb = TmdbProviderFactory.CreateClient(paths);
    }

    public string Name => "EmbyTMDBScraperFix TMDB Series";
    public int Order => 0;
    public string[] GetSupportedExternalIdentifiers() => new[] { "Tmdb" };
    public bool Supports(BaseItem item) => item is Series;

    public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(SeriesInfo info, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(info.Name)) return Array.Empty<RemoteSearchResult>();
        var cfg = Plugin.Instance?.Configuration ?? new PluginConfiguration();
        var search = await _tmdb.SearchSeriesAsync(info.Name, info.Year, cfg, cancellationToken).ConfigureAwait(false);
        return search?.Results?.Select(x => TmdbProviderFactory.ToRemoteSearchResult(x, Name, _tmdb.GetImageUrl(x.Poster_Path, "w500"), true)).ToArray()
               ?? Array.Empty<RemoteSearchResult>();
    }

    public async Task<MetadataResult<Series>> GetMetadata(SeriesInfo info, CancellationToken cancellationToken)
    {
        var result = new MetadataResult<Series>();
        var cfg = Plugin.Instance?.Configuration ?? new PluginConfiguration();
        var tmdbId = info.ProviderIds != null && info.ProviderIds.TryGetValue("Tmdb", out var id) ? id : null;
        TmdbTitle? data = !string.IsNullOrWhiteSpace(tmdbId)
            ? await _tmdb.GetSeriesAsync(tmdbId, cfg, cancellationToken).ConfigureAwait(false)
            : null;

        if (data == null)
        {
            var search = await GetSearchResults(info, cancellationToken).ConfigureAwait(false);
            var match = search.FirstOrDefault();
            if (match == null || !match.ProviderIds.TryGetValue("Tmdb", out tmdbId)) return result;
            data = await _tmdb.GetSeriesAsync(tmdbId, cfg, cancellationToken).ConfigureAwait(false);
        }

        if (data == null) return result;
        var item = new Series
        {
            Name = data.Name ?? data.Original_Name ?? info.Name,
            Overview = data.Overview,
            ProductionYear = TmdbProviderFactory.ParseYear(data.First_Air_Date) ?? info.Year,
            CommunityRating = (float?)data.Vote_Average,
            PremiereDate = TmdbProviderFactory.ParseDate(data.First_Air_Date)
        };
        item.ProviderIds["Tmdb"] = data.Id.ToString(CultureInfo.InvariantCulture);
        foreach (var genre in data.Genres.Where(g => !string.IsNullOrWhiteSpace(g.Name))) item.AddGenre(genre.Name!);
        result.Item = item;
        result.HasMetadata = true;
        result.Provider = Name;
        result.ResultLanguage = cfg.TmdbLanguage;
        return result;
    }

    public IEnumerable<ImageType> GetSupportedImages(BaseItem item) => new[] { ImageType.Primary, ImageType.Backdrop };

    public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, LibraryOptions libraryOptions, CancellationToken cancellationToken)
    {
        var cfg = Plugin.Instance?.Configuration ?? new PluginConfiguration();
        var tmdbId = item.ProviderIds.TryGetValue("Tmdb", out var id) ? id : null;
        if (string.IsNullOrWhiteSpace(tmdbId)) return Array.Empty<RemoteImageInfo>();
        var data = await _tmdb.GetSeriesAsync(tmdbId, cfg, cancellationToken).ConfigureAwait(false);
        if (data == null) return Array.Empty<RemoteImageInfo>();
        var images = new List<RemoteImageInfo>();
        if (!string.IsNullOrWhiteSpace(data.Poster_Path)) images.Add(new RemoteImageInfo { ProviderName = Name, Url = _tmdb.GetImageUrl(data.Poster_Path), ThumbnailUrl = _tmdb.GetImageUrl(data.Poster_Path, "w500"), Type = ImageType.Primary, Language = cfg.TmdbLanguage });
        if (!string.IsNullOrWhiteSpace(data.Backdrop_Path)) images.Add(new RemoteImageInfo { ProviderName = Name, Url = _tmdb.GetImageUrl(data.Backdrop_Path), ThumbnailUrl = _tmdb.GetImageUrl(data.Backdrop_Path, "w780"), Type = ImageType.Backdrop, Language = cfg.TmdbLanguage });
        return images;
    }

    public Task<HttpResponseInfo> GetImageResponse(string url, CancellationToken cancellationToken) => TmdbProviderFactory.GetImageResponseAsync(url, cancellationToken);
}

public sealed class TmdbEpisodeMetadataProvider : IRemoteMetadataProvider<EpisodeInfo, Episode>, IHasOrder, IHasSupportedExternalIdentifiers
{
    private readonly TmdbApiClient _tmdb;

    public TmdbEpisodeMetadataProvider(IApplicationPaths paths)
    {
        _tmdb = TmdbProviderFactory.CreateClient(paths);
    }

    public string Name => "EmbyTMDBScraperFix TMDB Episode";
    public int Order => 0;
    public string[] GetSupportedExternalIdentifiers() => new[] { "Tmdb" };

    public Task<IEnumerable<RemoteSearchResult>> GetSearchResults(EpisodeInfo info, CancellationToken cancellationToken)
    {
        return Task.FromResult<IEnumerable<RemoteSearchResult>>(Array.Empty<RemoteSearchResult>());
    }

    public async Task<MetadataResult<Episode>> GetMetadata(EpisodeInfo info, CancellationToken cancellationToken)
    {
        var result = new MetadataResult<Episode>();
        var cfg = Plugin.Instance?.Configuration ?? new PluginConfiguration();
        if (!info.ParentIndexNumber.HasValue || !info.IndexNumber.HasValue) return result;

        string? seriesTmdbId = null;
        if (info.SeriesProviderIds != null) info.SeriesProviderIds.TryGetValue("Tmdb", out seriesTmdbId);
        if (string.IsNullOrWhiteSpace(seriesTmdbId)) return result;

        var data = await _tmdb.GetEpisodeAsync(seriesTmdbId, info.ParentIndexNumber.Value, info.IndexNumber.Value, cfg, cancellationToken).ConfigureAwait(false);
        if (data == null) return result;

        var item = new Episode
        {
            Name = data.Name ?? info.Name,
            Overview = data.Overview,
            PremiereDate = TmdbProviderFactory.ParseDate(data.Air_Date),
            CommunityRating = (float?)data.Vote_Average
        };
        item.ProviderIds["Tmdb"] = data.Id.ToString(CultureInfo.InvariantCulture);
        result.Item = item;
        result.HasMetadata = true;
        result.Provider = Name;
        result.ResultLanguage = cfg.TmdbLanguage;
        return result;
    }

    public Task<HttpResponseInfo> GetImageResponse(string url, CancellationToken cancellationToken) => TmdbProviderFactory.GetImageResponseAsync(url, cancellationToken);
}
