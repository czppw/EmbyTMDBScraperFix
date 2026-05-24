using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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

internal static class MetadataProviderFactory
{
    public static TmdbApiClient CreateTmdbClient(IApplicationPaths paths)
    {
        var log = new PluginLogService(paths);
        var policy = new ProxyPolicyService(log);
        var http = new ProxyHttpClientService(policy, log);
        return new TmdbApiClient(http, log);
    }

    public static TvdbApiClient CreateTvdbClient(IApplicationPaths paths)
    {
        var log = new PluginLogService(paths);
        var policy = new ProxyPolicyService(log);
        var http = new ProxyHttpClientService(policy, log);
        return new TvdbApiClient(http, log);
    }

    public static Task<HttpResponseInfo> GetImageResponseAsync(string url, CancellationToken cancellationToken)
    {
        if (PluginRuntime.TryGetInstance(out var runtime))
        {
            return runtime.ProxyClient.GetResponseInfoAsync(url, Plugin.Instance?.Configuration ?? new PluginConfiguration(), cancellationToken);
        }

        throw new InvalidOperationException("EmbyTMDBScraperFix runtime is not initialized yet.");
    }

    public static RemoteSearchResult ToTmdbRemoteSearchResult(TmdbSearchItem item, string providerName, string? imageUrl, bool isSeries)
    {
        var date = isSeries ? item.First_Air_Date : item.Release_Date;
        var name = isSeries
            ? item.Name ?? item.Original_Name
            : item.Title ?? item.Original_Title ?? item.Name ?? item.Original_Name;
        var result = new RemoteSearchResult
        {
            Name = name ?? string.Empty,
            OriginalTitle = isSeries ? item.Original_Name : item.Original_Title ?? item.Original_Name,
            Overview = item.Overview,
            ProductionYear = ParseYear(date),
            PremiereDate = ParseDate(date),
            ImageUrl = imageUrl,
            SearchProviderName = providerName
        };
        result.ProviderIds["Tmdb"] = item.Id.ToString(CultureInfo.InvariantCulture);
        return result;
    }

    public static RemoteSearchResult ToTvdbRemoteSearchResult(TvdbSearchItem item, string providerName, string? imageUrl)
    {
        var result = new RemoteSearchResult
        {
            Name = item.Name ?? string.Empty,
            Overview = item.Overview,
            ProductionYear = ParseYear(item.Year),
            ImageUrl = imageUrl,
            SearchProviderName = providerName
        };
        var id = item.TvdbId ?? item.Id;
        if (!string.IsNullOrWhiteSpace(id)) result.ProviderIds["Tvdb"] = id;
        return result;
    }

    public static int? ParseYear(string? value) => DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt) ? dt.Year : int.TryParse(value, out var year) ? year : null;
    public static DateTimeOffset? ParseDate(string? value) => DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dt) ? dt : null;

    public static IEnumerable<RemoteImageInfo> BuildRemoteImages(string providerName, string language, params (string url, string thumb, ImageType type)[] defs)
    {
        foreach (var (url, thumb, type) in defs)
        {
            if (!string.IsNullOrWhiteSpace(url))
            {
                yield return new RemoteImageInfo
                {
                    ProviderName = providerName,
                    Url = url,
                    ThumbnailUrl = string.IsNullOrWhiteSpace(thumb) ? url : thumb,
                    Type = type,
                    Language = language
                };
            }
        }
    }

    public static void ApplyTmdbPeople(BaseMetadataResult result, TmdbCredits? credits, TmdbApiClient tmdb)
    {
        result.ResetPeople();
        if (credits == null)
        {
            return;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var cast in credits.Cast.OrderBy(x => x.Order).Take(30))
        {
            var name = cast.Name?.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var dedupeKey = "cast|" + cast.Id.ToString(CultureInfo.InvariantCulture) + "|" + name + "|" + (cast.Character ?? string.Empty);
            if (!seen.Add(dedupeKey))
            {
                continue;
            }

            var person = new PersonInfo
            {
                Name = name,
                Role = cast.Character ?? string.Empty,
                Type = PersonType.Actor,
                ImageUrl = tmdb.GetImageUrl(cast.Profile_Path, "w300"),
                ProviderIds = new ProviderIdDictionary()
            };
            if (cast.Id > 0)
            {
                person.ProviderIds["Tmdb"] = cast.Id.ToString(CultureInfo.InvariantCulture);
            }

            result.AddPerson(person);
        }

        foreach (var crew in credits.Crew)
        {
            var personType = ToCrewPersonType(crew.Job);
            if (!personType.HasValue)
            {
                continue;
            }

            var name = crew.Name?.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var dedupeKey = "crew|" + personType.Value + "|" + crew.Id.ToString(CultureInfo.InvariantCulture) + "|" + name;
            if (!seen.Add(dedupeKey))
            {
                continue;
            }

            var person = new PersonInfo
            {
                Name = name,
                Role = crew.Job ?? string.Empty,
                Type = personType.Value,
                ImageUrl = tmdb.GetImageUrl(crew.Profile_Path, "w300"),
                ProviderIds = new ProviderIdDictionary()
            };
            if (crew.Id > 0)
            {
                person.ProviderIds["Tmdb"] = crew.Id.ToString(CultureInfo.InvariantCulture);
            }

            result.AddPerson(person);
        }
    }

    public static void MergeMissingTmdbTitleFields(TmdbTitle target, TmdbTitle? fallback)
    {
        if (fallback == null)
        {
            return;
        }

        target.Title = PreferNonEmpty(target.Title, fallback.Title);
        target.Name = PreferNonEmpty(target.Name, fallback.Name);
        target.Original_Title = PreferNonEmpty(target.Original_Title, fallback.Original_Title);
        target.Original_Name = PreferNonEmpty(target.Original_Name, fallback.Original_Name);
        target.Overview = PreferNonEmpty(target.Overview, fallback.Overview);
        target.Release_Date = PreferNonEmpty(target.Release_Date, fallback.Release_Date);
        target.First_Air_Date = PreferNonEmpty(target.First_Air_Date, fallback.First_Air_Date);
        target.Air_Date = PreferNonEmpty(target.Air_Date, fallback.Air_Date);
        target.Poster_Path = PreferNonEmpty(target.Poster_Path, fallback.Poster_Path);
        target.Backdrop_Path = PreferNonEmpty(target.Backdrop_Path, fallback.Backdrop_Path);

        if (target.Runtime == null)
        {
            target.Runtime = fallback.Runtime;
        }

        if (target.Genres.Count == 0 && fallback.Genres.Count > 0)
        {
            target.Genres = fallback.Genres;
        }

        if ((target.Credits == null || (target.Credits.Cast.Count == 0 && target.Credits.Crew.Count == 0)) && fallback.Credits != null)
        {
            target.Credits = fallback.Credits;
        }
    }

    public static void MergeMissingTmdbPersonFields(TmdbPerson target, TmdbPerson? fallback)
    {
        if (fallback == null)
        {
            return;
        }

        target.Name = PreferNonEmpty(target.Name, fallback.Name);
        target.Biography = PreferNonEmpty(target.Biography, fallback.Biography);
        target.Birthday = PreferNonEmpty(target.Birthday, fallback.Birthday);
        target.Profile_Path = PreferNonEmpty(target.Profile_Path, fallback.Profile_Path);
    }

    public static string? PreferNonEmpty(string? primary, string? fallback)
        => string.IsNullOrWhiteSpace(primary) ? fallback : primary;

    public static string ResolveLookupName(string? explicitName, string? path)
    {
        if (!string.IsNullOrWhiteSpace(explicitName))
        {
            return explicitName.Trim();
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var trimmedPath = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (string.IsNullOrWhiteSpace(trimmedPath))
        {
            return string.Empty;
        }

        var candidate = Path.GetFileName(trimmedPath);
        if (LooksLikeSeasonFolder(candidate))
        {
            var parent = Path.GetDirectoryName(trimmedPath);
            if (!string.IsNullOrWhiteSpace(parent))
            {
                candidate = Path.GetFileName(parent);
            }
        }

        if (candidate.IndexOf('.') >= 0 || candidate.IndexOf('_') >= 0)
        {
            candidate = candidate.Replace('.', ' ').Replace('_', ' ');
        }

        return candidate?.Trim() ?? string.Empty;
    }

    private static bool LooksLikeSeasonFolder(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim().ToLowerInvariant();
        return normalized.StartsWith("season ", StringComparison.Ordinal)
            || normalized.StartsWith("season_", StringComparison.Ordinal)
            || normalized.StartsWith("s", StringComparison.Ordinal) && normalized.Length <= 4 && normalized.Skip(1).All(char.IsDigit)
            || normalized.StartsWith("第", StringComparison.Ordinal) && normalized.EndsWith("季", StringComparison.Ordinal);
    }

    private static PersonType? ToCrewPersonType(string? job)
    {
        if (string.IsNullOrWhiteSpace(job))
        {
            return null;
        }

        if (job.Equals("Director", StringComparison.OrdinalIgnoreCase))
        {
            return PersonType.Director;
        }

        if (job.Equals("Writer", StringComparison.OrdinalIgnoreCase)
            || job.Equals("Screenplay", StringComparison.OrdinalIgnoreCase)
            || job.Equals("Story", StringComparison.OrdinalIgnoreCase))
        {
            return PersonType.Writer;
        }

        if (job.Equals("Producer", StringComparison.OrdinalIgnoreCase)
            || job.Equals("Executive Producer", StringComparison.OrdinalIgnoreCase))
        {
            return PersonType.Producer;
        }

        if (job.Equals("Original Music Composer", StringComparison.OrdinalIgnoreCase)
            || job.Equals("Composer", StringComparison.OrdinalIgnoreCase))
        {
            return PersonType.Composer;
        }

        if (job.Equals("Conductor", StringComparison.OrdinalIgnoreCase))
        {
            return PersonType.Conductor;
        }

        if (job.Equals("Lyricist", StringComparison.OrdinalIgnoreCase))
        {
            return PersonType.Lyricist;
        }

        return null;
    }
}

public sealed class TmdbMovieMetadataProvider : IRemoteMetadataProvider<Movie, MovieInfo>, IRemoteImageProvider, IHasOrder, IHasSupportedExternalIdentifiers
{
    private readonly TmdbApiClient _tmdb;

    public TmdbMovieMetadataProvider(IApplicationPaths paths)
    {
        _tmdb = MetadataProviderFactory.CreateTmdbClient(paths);
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
        return search?.Results?.Select(x => MetadataProviderFactory.ToTmdbRemoteSearchResult(x, Name, _tmdb.GetImageUrl(x.Poster_Path, "w500"), false)).ToArray()
               ?? Array.Empty<RemoteSearchResult>();
    }

    public async Task<MetadataResult<Movie>> GetMetadata(MovieInfo info, CancellationToken cancellationToken)
    {
        var result = new MetadataResult<Movie>();
        var cfg = Plugin.Instance?.Configuration ?? new PluginConfiguration();
        var tmdbId = info.ProviderIds != null && info.ProviderIds.TryGetValue("Tmdb", out var id) ? id : null;
        if (string.IsNullOrWhiteSpace(tmdbId))
        {
            var search = await GetSearchResults(info, cancellationToken).ConfigureAwait(false);
            var match = search.FirstOrDefault();
            if (match == null || !match.ProviderIds.TryGetValue("Tmdb", out tmdbId) || string.IsNullOrWhiteSpace(tmdbId)) return result;
        }

        var data = await _tmdb.GetMovieAsync(tmdbId, cfg, cancellationToken).ConfigureAwait(false);

        if (data == null)
        {
            var search = await GetSearchResults(info, cancellationToken).ConfigureAwait(false);
            var match = search.FirstOrDefault();
            if (match == null || !match.ProviderIds.TryGetValue("Tmdb", out tmdbId) || string.IsNullOrWhiteSpace(tmdbId)) return result;
            data = await _tmdb.GetMovieAsync(tmdbId, cfg, cancellationToken).ConfigureAwait(false);
        }

        if (data == null) return result;
        if (string.IsNullOrWhiteSpace(data.Overview) || (data.Credits?.Cast.Count ?? 0) == 0)
        {
            var fallback = await _tmdb.GetMovieAsync(tmdbId, cfg, cancellationToken, omitConfiguredLanguage: true).ConfigureAwait(false);
            MetadataProviderFactory.MergeMissingTmdbTitleFields(data, fallback);
        }

        var item = new Movie
        {
            Name = data.Title ?? data.Original_Title ?? info.Name,
            Overview = data.Overview,
            ProductionYear = MetadataProviderFactory.ParseYear(data.Release_Date) ?? info.Year,
            CommunityRating = (float?)data.Vote_Average,
            PremiereDate = MetadataProviderFactory.ParseDate(data.Release_Date)
        };
        item.ProviderIds["Tmdb"] = data.Id.ToString(CultureInfo.InvariantCulture);
        foreach (var genre in data.Genres.Where(g => !string.IsNullOrWhiteSpace(g.Name))) item.AddGenre(genre.Name!);
        result.Item = item;
        MetadataProviderFactory.ApplyTmdbPeople(result, data.Credits, _tmdb);
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
        if (data == null) return Array.Empty<RemoteImageInfo>();
        return MetadataProviderFactory.BuildRemoteImages(Name, cfg.TmdbLanguage,
            (_tmdb.GetImageUrl(data.Poster_Path), _tmdb.GetImageUrl(data.Poster_Path, "w500"), ImageType.Primary),
            (_tmdb.GetImageUrl(data.Backdrop_Path), _tmdb.GetImageUrl(data.Backdrop_Path, "w780"), ImageType.Backdrop)).ToArray();
    }

    public Task<HttpResponseInfo> GetImageResponse(string url, CancellationToken cancellationToken) => MetadataProviderFactory.GetImageResponseAsync(url, cancellationToken);
}

public sealed class TmdbSeriesMetadataProvider : IRemoteMetadataProvider<Series, SeriesInfo>, IRemoteImageProvider, IHasOrder, IHasSupportedExternalIdentifiers
{
    private readonly TmdbApiClient _tmdb;
    private readonly TvdbApiClient _tvdb;
    private readonly PluginLogService? _log;

    public TmdbSeriesMetadataProvider(IApplicationPaths paths)
    {
        _tmdb = MetadataProviderFactory.CreateTmdbClient(paths);
        _tvdb = MetadataProviderFactory.CreateTvdbClient(paths);
        _log = PluginRuntime.TryGetInstance(out var runtime) ? runtime.Log : new PluginLogService(paths);
    }

    public string Name => "EmbyTMDBScraperFix TMDB Series";
    public int Order => 0;
    public string[] GetSupportedExternalIdentifiers() => new[] { "Tmdb", "Tvdb" };
    public bool Supports(BaseItem item) => item is Series;

    public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(SeriesInfo info, CancellationToken cancellationToken)
    {
        var lookupName = MetadataProviderFactory.ResolveLookupName(info.Name, info.Path);
        if (string.IsNullOrWhiteSpace(lookupName))
        {
            _log?.Warn($"TMDB series search skipped because both SeriesInfo.Name and SeriesInfo.Path were empty. Path='{info.Path ?? string.Empty}'");
            return Array.Empty<RemoteSearchResult>();
        }

        var cfg = Plugin.Instance?.Configuration ?? new PluginConfiguration();
        _log?.Info($"TMDB series search start. Name='{lookupName}', Path='{info.Path ?? string.Empty}', Year={(info.Year.HasValue ? info.Year.Value.ToString(CultureInfo.InvariantCulture) : "null")}");
        var tmdbSearch = await _tmdb.SearchSeriesAsync(lookupName, info.Year, cfg, cancellationToken).ConfigureAwait(false);
        var results = tmdbSearch?.Results?.Select(x => MetadataProviderFactory.ToTmdbRemoteSearchResult(x, Name, _tmdb.GetImageUrl(x.Poster_Path, "w500"), true)).ToList()
            ?? new List<RemoteSearchResult>();
        _log?.Info($"TMDB series search finished. Name='{lookupName}', Results={results.Count}");

        if (results.Count == 0 && cfg.EnableTvdbFallback && !string.IsNullOrWhiteSpace(cfg.TvdbApiKey))
        {
            var tvdbSearch = await _tvdb.SearchSeriesAsync(lookupName, cfg, cancellationToken).ConfigureAwait(false);
            if (tvdbSearch?.Data != null)
            {
                results.AddRange(tvdbSearch.Data.Select(x => MetadataProviderFactory.ToTvdbRemoteSearchResult(x, Name, _tvdb.NormalizeImageUrl(x.ImageUrl))));
                _log?.Info($"TVDB series fallback search finished. Name='{lookupName}', Results={tvdbSearch.Data.Count}");
            }
        }

        return results;
    }

    public async Task<MetadataResult<Series>> GetMetadata(SeriesInfo info, CancellationToken cancellationToken)
    {
        var result = new MetadataResult<Series>();
        var cfg = Plugin.Instance?.Configuration ?? new PluginConfiguration();
        var lookupName = MetadataProviderFactory.ResolveLookupName(info.Name, info.Path);
        var tmdbId = info.ProviderIds != null && info.ProviderIds.TryGetValue("Tmdb", out var id) ? id : null;
        _log?.Info($"TMDB series metadata start. Name='{lookupName}', Path='{info.Path ?? string.Empty}', ExistingTmdbId='{tmdbId ?? string.Empty}', Year={(info.Year.HasValue ? info.Year.Value.ToString(CultureInfo.InvariantCulture) : "null")}");
        if (string.IsNullOrWhiteSpace(tmdbId))
        {
            var tmdbSearch = await GetSearchResults(info, cancellationToken).ConfigureAwait(false);
            var firstTmdb = tmdbSearch.FirstOrDefault(x => x.ProviderIds.ContainsKey("Tmdb"));
            if (firstTmdb != null) firstTmdb.ProviderIds.TryGetValue("Tmdb", out tmdbId);
            if (string.IsNullOrWhiteSpace(tmdbId))
            {
                _log?.Warn($"TMDB series metadata search did not resolve an id. Name='{lookupName}', Path='{info.Path ?? string.Empty}'");
            }
        }

        if (!string.IsNullOrWhiteSpace(tmdbId))
        {
            var data = await _tmdb.GetSeriesAsync(tmdbId, cfg, cancellationToken).ConfigureAwait(false);
            if (data != null)
            {
                if (string.IsNullOrWhiteSpace(data.Overview) || (data.Credits?.Cast.Count ?? 0) == 0)
                {
                    var fallback = await _tmdb.GetSeriesAsync(tmdbId, cfg, cancellationToken, omitConfiguredLanguage: true).ConfigureAwait(false);
                    MetadataProviderFactory.MergeMissingTmdbTitleFields(data, fallback);
                }

                var item = new Series
                {
                    Name = data.Name ?? data.Original_Name ?? info.Name,
                    Overview = data.Overview,
                    ProductionYear = MetadataProviderFactory.ParseYear(data.First_Air_Date) ?? info.Year,
                    CommunityRating = (float?)data.Vote_Average,
                    PremiereDate = MetadataProviderFactory.ParseDate(data.First_Air_Date)
                };
                item.ProviderIds["Tmdb"] = data.Id.ToString(CultureInfo.InvariantCulture);
                foreach (var genre in data.Genres.Where(g => !string.IsNullOrWhiteSpace(g.Name))) item.AddGenre(genre.Name!);
                result.Item = item;
                MetadataProviderFactory.ApplyTmdbPeople(result, data.Credits, _tmdb);
                result.HasMetadata = true;
                result.Provider = Name;
                result.ResultLanguage = cfg.TmdbLanguage;
                _log?.Info($"TMDB series metadata completed. Name='{lookupName}', TmdbId='{tmdbId}'");
                return result;
            }

            _log?.Warn($"TMDB series metadata fetch returned null. Name='{lookupName}', TmdbId='{tmdbId}'");
        }

        if (cfg.EnableTvdbFallback && !string.IsNullOrWhiteSpace(cfg.TvdbApiKey))
        {
            var tvdbId = info.ProviderIds != null && info.ProviderIds.TryGetValue("Tvdb", out var tvdbProviderId) ? tvdbProviderId : null;
            if (string.IsNullOrWhiteSpace(tvdbId))
            {
                var search = await _tvdb.SearchSeriesAsync(lookupName, cfg, cancellationToken).ConfigureAwait(false);
                var first = search?.Data?.FirstOrDefault();
                tvdbId = first?.TvdbId ?? first?.Id;
            }

            if (!string.IsNullOrWhiteSpace(tvdbId))
            {
                var safeTvdbId = tvdbId;
                var tvdbData = await _tvdb.GetSeriesAsync(safeTvdbId, cfg, cancellationToken).ConfigureAwait(false);
                if (tvdbData?.Data != null)
                {
                    var item = new Series
                    {
                        Name = tvdbData.Data.Name ?? info.Name,
                        Overview = tvdbData.Data.Overview,
                        ProductionYear = MetadataProviderFactory.ParseYear(tvdbData.Data.Year),
                        PremiereDate = MetadataProviderFactory.ParseDate(tvdbData.Data.FirstAired)
                    };
                    item.ProviderIds["Tvdb"] = tvdbData.Data.Id.ToString(CultureInfo.InvariantCulture);
                    result.Item = item;
                    result.HasMetadata = true;
                    result.Provider = Name + " (TVDB fallback)";
                    result.ResultLanguage = cfg.TvdbLanguage;
                    _log?.Info($"TVDB series metadata completed. Name='{lookupName}', TvdbId='{tvdbId}'");
                    return result;
                }
            }
        }

        _log?.Warn($"Series metadata lookup finished without a result. Name='{lookupName}', Path='{info.Path ?? string.Empty}'");
        return result;
    }

    public IEnumerable<ImageType> GetSupportedImages(BaseItem item) => new[] { ImageType.Primary, ImageType.Backdrop };

    public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, LibraryOptions libraryOptions, CancellationToken cancellationToken)
    {
        var cfg = Plugin.Instance?.Configuration ?? new PluginConfiguration();
        var tmdbId = item.ProviderIds.TryGetValue("Tmdb", out var id) ? id : null;
        if (!string.IsNullOrWhiteSpace(tmdbId))
        {
            var data = await _tmdb.GetSeriesAsync(tmdbId, cfg, cancellationToken).ConfigureAwait(false);
            if (data != null)
            {
                return MetadataProviderFactory.BuildRemoteImages(Name, cfg.TmdbLanguage,
                    (_tmdb.GetImageUrl(data.Poster_Path), _tmdb.GetImageUrl(data.Poster_Path, "w500"), ImageType.Primary),
                    (_tmdb.GetImageUrl(data.Backdrop_Path), _tmdb.GetImageUrl(data.Backdrop_Path, "w780"), ImageType.Backdrop)).ToArray();
            }
        }

        var tvdbId = item.ProviderIds.TryGetValue("Tvdb", out var tvdbProviderId) ? tvdbProviderId : null;
        if (!string.IsNullOrWhiteSpace(tvdbId) && cfg.EnableTvdbFallback)
        {
            var safeTvdbId = tvdbId;
            var data = await _tvdb.GetSeriesAsync(safeTvdbId, cfg, cancellationToken).ConfigureAwait(false);
            if (data?.Data != null)
            {
                return MetadataProviderFactory.BuildRemoteImages(Name, cfg.TvdbLanguage,
                    (_tvdb.NormalizeImageUrl(data.Data.Image), _tvdb.NormalizeImageUrl(data.Data.Image), ImageType.Primary)).ToArray();
            }
        }

        return Array.Empty<RemoteImageInfo>();
    }

    public Task<HttpResponseInfo> GetImageResponse(string url, CancellationToken cancellationToken) => MetadataProviderFactory.GetImageResponseAsync(url, cancellationToken);
}

public sealed class TmdbSeasonMetadataProvider : IRemoteMetadataProvider<Season, SeasonInfo>, IRemoteImageProvider, IHasOrder, IHasSupportedExternalIdentifiers
{
    private readonly TmdbApiClient _tmdb;
    private readonly TvdbApiClient _tvdb;

    public TmdbSeasonMetadataProvider(IApplicationPaths paths)
    {
        _tmdb = MetadataProviderFactory.CreateTmdbClient(paths);
        _tvdb = MetadataProviderFactory.CreateTvdbClient(paths);
    }

    public string Name => "EmbyTMDBScraperFix TMDB Season";
    public int Order => 0;
    public string[] GetSupportedExternalIdentifiers() => new[] { "Tmdb", "Tvdb" };
    public bool Supports(BaseItem item) => item is Season;

    public Task<IEnumerable<RemoteSearchResult>> GetSearchResults(SeasonInfo info, CancellationToken cancellationToken)
    {
        return Task.FromResult<IEnumerable<RemoteSearchResult>>(Array.Empty<RemoteSearchResult>());
    }

    public async Task<MetadataResult<Season>> GetMetadata(SeasonInfo info, CancellationToken cancellationToken)
    {
        var result = new MetadataResult<Season>();
        var cfg = Plugin.Instance?.Configuration ?? new PluginConfiguration();
        if (!info.IndexNumber.HasValue) return result;

        string? tmdbSeriesId = null;
        if (info.SeriesProviderIds != null) info.SeriesProviderIds.TryGetValue("Tmdb", out tmdbSeriesId);
        if (string.IsNullOrWhiteSpace(tmdbSeriesId) && !string.IsNullOrWhiteSpace(info.SeriesName))
        {
            var search = await _tmdb.SearchSeriesAsync(info.SeriesName, info.Year, cfg, cancellationToken).ConfigureAwait(false);
            var firstTmdb = search?.Results?.FirstOrDefault();
            tmdbSeriesId = firstTmdb != null ? firstTmdb.Id.ToString(CultureInfo.InvariantCulture) : null;
        }

        if (!string.IsNullOrWhiteSpace(tmdbSeriesId))
        {
            var safeTmdbSeriesId = tmdbSeriesId;
            var data = await _tmdb.GetSeasonAsync(safeTmdbSeriesId, info.IndexNumber.Value, cfg, cancellationToken).ConfigureAwait(false);
            if (data != null)
            {
                var item = new Season
                {
                    Name = data.Name ?? info.Name,
                    Overview = data.Overview,
                    ProductionYear = MetadataProviderFactory.ParseYear(data.Air_Date) ?? info.Year,
                    PremiereDate = MetadataProviderFactory.ParseDate(data.Air_Date),
                    SeriesName = info.SeriesName ?? string.Empty
                };
                item.ProviderIds["Tmdb"] = data.Id.ToString(CultureInfo.InvariantCulture);
                result.Item = item;
                result.HasMetadata = true;
                result.Provider = Name;
                result.ResultLanguage = cfg.TmdbLanguage;
                return result;
            }
        }

        if (cfg.EnableTvdbFallback)
        {
            string? tvdbSeriesId = null;
            if (info.SeriesProviderIds != null) info.SeriesProviderIds.TryGetValue("Tvdb", out tvdbSeriesId);
            if (string.IsNullOrWhiteSpace(tvdbSeriesId) && !string.IsNullOrWhiteSpace(info.SeriesName))
            {
                var search = await _tvdb.SearchSeriesAsync(info.SeriesName, cfg, cancellationToken).ConfigureAwait(false);
                var first = search?.Data?.FirstOrDefault();
                tvdbSeriesId = first?.TvdbId ?? first?.Id;
            }

            if (!string.IsNullOrWhiteSpace(tvdbSeriesId))
            {
                var safeTvdbSeriesId = tvdbSeriesId;
                var series = await _tvdb.GetSeriesAsync(safeTvdbSeriesId, cfg, cancellationToken).ConfigureAwait(false);
                var seasonSummary = series?.Data?.Seasons?.FirstOrDefault(x => x.Number == info.IndexNumber.Value);
                if (seasonSummary != null)
                {
                    var season = await _tvdb.GetSeasonAsync(seasonSummary.Id.ToString(CultureInfo.InvariantCulture), cfg, cancellationToken).ConfigureAwait(false);
                    if (season?.Data != null)
                    {
                        var item = new Season
                        {
                            Name = season.Data.Name ?? info.Name,
                            Overview = season.Data.Overview,
                            SeriesName = info.SeriesName ?? string.Empty
                        };
                        item.ProviderIds["Tvdb"] = season.Data.Id.ToString(CultureInfo.InvariantCulture);
                        result.Item = item;
                        result.HasMetadata = true;
                        result.Provider = Name + " (TVDB fallback)";
                        result.ResultLanguage = cfg.TvdbLanguage;
                        return result;
                    }
                }
            }
        }

        return result;
    }

    public IEnumerable<ImageType> GetSupportedImages(BaseItem item) => new[] { ImageType.Primary };

    public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, LibraryOptions libraryOptions, CancellationToken cancellationToken)
    {
        var cfg = Plugin.Instance?.Configuration ?? new PluginConfiguration();
        var tmdbId = item.ProviderIds.TryGetValue("Tmdb", out var id) ? id : null;
        if (!string.IsNullOrWhiteSpace(tmdbId) && item is Season seasonItem && seasonItem.IndexNumber.HasValue)
        {
            var tmdbSeriesId = seasonItem.Series?.ProviderIds.TryGetValue("Tmdb", out var sid) == true ? sid : null;
            if (!string.IsNullOrWhiteSpace(tmdbSeriesId))
            {
                var safeTmdbSeriesId = tmdbSeriesId;
                var data = await _tmdb.GetSeasonAsync(safeTmdbSeriesId, seasonItem.IndexNumber.Value, cfg, cancellationToken).ConfigureAwait(false);
                if (data != null)
                {
                    return MetadataProviderFactory.BuildRemoteImages(Name, cfg.TmdbLanguage,
                        (_tmdb.GetImageUrl(data.Poster_Path), _tmdb.GetImageUrl(data.Poster_Path, "w500"), ImageType.Primary)).ToArray();
                }
            }
        }

        var tvdbId = item.ProviderIds.TryGetValue("Tvdb", out var tvdbProviderId) ? tvdbProviderId : null;
        if (!string.IsNullOrWhiteSpace(tvdbId) && cfg.EnableTvdbFallback)
        {
            var safeTvdbId = tvdbId;
            var season = await _tvdb.GetSeasonAsync(safeTvdbId, cfg, cancellationToken).ConfigureAwait(false);
            if (season?.Data != null)
            {
                return MetadataProviderFactory.BuildRemoteImages(Name, cfg.TvdbLanguage,
                    (_tvdb.NormalizeImageUrl(season.Data.Image), _tvdb.NormalizeImageUrl(season.Data.Image), ImageType.Primary)).ToArray();
            }
        }

        return Array.Empty<RemoteImageInfo>();
    }

    public Task<HttpResponseInfo> GetImageResponse(string url, CancellationToken cancellationToken) => MetadataProviderFactory.GetImageResponseAsync(url, cancellationToken);
}

public sealed class TmdbEpisodeMetadataProvider : IRemoteMetadataProvider<Episode, EpisodeInfo>, IHasOrder, IHasSupportedExternalIdentifiers
{
    private readonly TmdbApiClient _tmdb;
    private readonly TvdbApiClient _tvdb;

    public TmdbEpisodeMetadataProvider(IApplicationPaths paths)
    {
        _tmdb = MetadataProviderFactory.CreateTmdbClient(paths);
        _tvdb = MetadataProviderFactory.CreateTvdbClient(paths);
    }

    public string Name => "EmbyTMDBScraperFix TMDB Episode";
    public int Order => 0;
    public string[] GetSupportedExternalIdentifiers() => new[] { "Tmdb", "Tvdb" };

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
        if (!string.IsNullOrWhiteSpace(seriesTmdbId))
        {
            var safeSeriesTmdbId = seriesTmdbId;
            var data = await _tmdb.GetEpisodeAsync(safeSeriesTmdbId, info.ParentIndexNumber.Value, info.IndexNumber.Value, cfg, cancellationToken).ConfigureAwait(false);
            if (data != null)
            {
                var item = new Episode
                {
                    Name = data.Name ?? info.Name,
                    Overview = data.Overview,
                    PremiereDate = MetadataProviderFactory.ParseDate(data.Air_Date),
                    CommunityRating = (float?)data.Vote_Average
                };
                item.ProviderIds["Tmdb"] = data.Id.ToString(CultureInfo.InvariantCulture);
                result.Item = item;
                result.HasMetadata = true;
                result.Provider = Name;
                result.ResultLanguage = cfg.TmdbLanguage;
                return result;
            }
        }

        if (cfg.EnableTvdbFallback)
        {
            string? tvdbSeriesId = null;
            if (info.SeriesProviderIds != null) info.SeriesProviderIds.TryGetValue("Tvdb", out tvdbSeriesId);
            if (string.IsNullOrWhiteSpace(tvdbSeriesId) && !string.IsNullOrWhiteSpace(info.Name))
            {
                var seriesSearch = await _tvdb.SearchSeriesAsync(info.Name, cfg, cancellationToken).ConfigureAwait(false);
                var firstSeries = seriesSearch?.Data?.FirstOrDefault();
                tvdbSeriesId = firstSeries?.TvdbId ?? firstSeries?.Id;
            }

            if (!string.IsNullOrWhiteSpace(tvdbSeriesId))
            {
                var safeTvdbSeriesId = tvdbSeriesId;
                var series = await _tvdb.GetSeriesAsync(safeTvdbSeriesId, cfg, cancellationToken).ConfigureAwait(false);
                var seasonSummary = series?.Data?.Seasons?.FirstOrDefault(x => x.Number == info.ParentIndexNumber.Value);
                if (seasonSummary != null)
                {
                    var season = await _tvdb.GetSeasonAsync(seasonSummary.Id.ToString(CultureInfo.InvariantCulture), cfg, cancellationToken).ConfigureAwait(false);
                    var episodeSummary = season?.Data?.Episodes?.FirstOrDefault(x => x.Number == info.IndexNumber.Value);
                    if (episodeSummary != null)
                    {
                        var episode = await _tvdb.GetEpisodeAsync(episodeSummary.Id.ToString(CultureInfo.InvariantCulture), cfg, cancellationToken).ConfigureAwait(false);
                        if (episode?.Data != null)
                        {
                            var item = new Episode
                            {
                                Name = episode.Data.Name ?? info.Name,
                                Overview = episode.Data.Overview,
                                PremiereDate = MetadataProviderFactory.ParseDate(episode.Data.Aired)
                            };
                            item.ProviderIds["Tvdb"] = episode.Data.Id.ToString(CultureInfo.InvariantCulture);
                            result.Item = item;
                            result.HasMetadata = true;
                            result.Provider = Name + " (TVDB fallback)";
                            result.ResultLanguage = cfg.TvdbLanguage;
                            return result;
                        }
                    }
                }
            }
        }

        return result;
    }

    public Task<HttpResponseInfo> GetImageResponse(string url, CancellationToken cancellationToken) => MetadataProviderFactory.GetImageResponseAsync(url, cancellationToken);
}

public sealed class TmdbPersonMetadataProvider : IRemoteMetadataProvider<Person, PersonLookupInfo>, IRemoteImageProvider, IHasOrder, IHasSupportedExternalIdentifiers
{
    private readonly TmdbApiClient _tmdb;
    private readonly TvdbApiClient _tvdb;

    public TmdbPersonMetadataProvider(IApplicationPaths paths)
    {
        _tmdb = MetadataProviderFactory.CreateTmdbClient(paths);
        _tvdb = MetadataProviderFactory.CreateTvdbClient(paths);
    }

    public string Name => "EmbyTMDBScraperFix TMDB Person";
    public int Order => 0;
    public string[] GetSupportedExternalIdentifiers() => new[] { "Tmdb", "Tvdb" };
    public bool Supports(BaseItem item) => item is Person;

    public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(PersonLookupInfo info, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(info.Name)) return Array.Empty<RemoteSearchResult>();
        var cfg = Plugin.Instance?.Configuration ?? new PluginConfiguration();
        var tmdbSearch = await _tmdb.SearchPersonAsync(info.Name, cfg, cancellationToken).ConfigureAwait(false);
        var results = tmdbSearch?.Results?.Select(x => MetadataProviderFactory.ToTmdbRemoteSearchResult(x, Name, _tmdb.GetImageUrl(x.Profile_Path, "w500"), false)).ToList()
            ?? new List<RemoteSearchResult>();

        if (results.Count == 0 && cfg.EnableTvdbFallback && !string.IsNullOrWhiteSpace(cfg.TvdbApiKey))
        {
            var tvdbSearch = await _tvdb.SearchPersonAsync(info.Name, cfg, cancellationToken).ConfigureAwait(false);
            if (tvdbSearch?.Data != null)
            {
                results.AddRange(tvdbSearch.Data.Select(x => MetadataProviderFactory.ToTvdbRemoteSearchResult(x, Name, _tvdb.NormalizeImageUrl(x.ImageUrl))));
            }
        }

        return results;
    }

    public async Task<MetadataResult<Person>> GetMetadata(PersonLookupInfo info, CancellationToken cancellationToken)
    {
        var result = new MetadataResult<Person>();
        var cfg = Plugin.Instance?.Configuration ?? new PluginConfiguration();
        var tmdbId = info.ProviderIds != null && info.ProviderIds.TryGetValue("Tmdb", out var id) ? id : null;
        if (string.IsNullOrWhiteSpace(tmdbId) && !string.IsNullOrWhiteSpace(info.Name))
        {
            var tmdbSearch = await GetSearchResults(info, cancellationToken).ConfigureAwait(false);
            var firstTmdb = tmdbSearch.FirstOrDefault(x => x.ProviderIds.ContainsKey("Tmdb"));
            if (firstTmdb != null) firstTmdb.ProviderIds.TryGetValue("Tmdb", out tmdbId);
        }

        if (!string.IsNullOrWhiteSpace(tmdbId))
        {
            var safeTmdbId = tmdbId;
            var data = await _tmdb.GetPersonAsync(safeTmdbId, cfg, cancellationToken).ConfigureAwait(false);
            if (data != null)
            {
                if (string.IsNullOrWhiteSpace(data.Biography))
                {
                    var fallback = await _tmdb.GetPersonAsync(safeTmdbId, cfg, cancellationToken, omitConfiguredLanguage: true).ConfigureAwait(false);
                    MetadataProviderFactory.MergeMissingTmdbPersonFields(data, fallback);
                }

                var item = new Person
                {
                    Name = data.Name ?? info.Name,
                    Overview = data.Biography,
                    ProductionYear = MetadataProviderFactory.ParseYear(data.Birthday)
                };
                item.ProviderIds["Tmdb"] = data.Id.ToString(CultureInfo.InvariantCulture);
                result.Item = item;
                result.HasMetadata = true;
                result.Provider = Name;
                result.ResultLanguage = cfg.TmdbLanguage;
                return result;
            }
        }

        if (cfg.EnableTvdbFallback && !string.IsNullOrWhiteSpace(cfg.TvdbApiKey))
        {
            var tvdbId = info.ProviderIds != null && info.ProviderIds.TryGetValue("Tvdb", out var tvdbProviderId) ? tvdbProviderId : null;
            if (string.IsNullOrWhiteSpace(tvdbId))
            {
                var search = await _tvdb.SearchPersonAsync(info.Name ?? string.Empty, cfg, cancellationToken).ConfigureAwait(false);
                var first = search?.Data?.FirstOrDefault();
                tvdbId = first?.TvdbId ?? first?.Id;
            }

            if (!string.IsNullOrWhiteSpace(tvdbId))
            {
                var safeTvdbId = tvdbId;
                var person = await _tvdb.GetPersonAsync(safeTvdbId, cfg, cancellationToken).ConfigureAwait(false);
                if (person?.Data != null)
                {
                    var item = new Person
                    {
                        Name = person.Data.Name ?? info.Name,
                        Overview = person.Data.Overview,
                        ProductionYear = MetadataProviderFactory.ParseYear(person.Data.Birth)
                    };
                    item.ProviderIds["Tvdb"] = person.Data.Id.ToString(CultureInfo.InvariantCulture);
                    result.Item = item;
                    result.HasMetadata = true;
                    result.Provider = Name + " (TVDB fallback)";
                    result.ResultLanguage = cfg.TvdbLanguage;
                    return result;
                }
            }
        }

        return result;
    }

    public IEnumerable<ImageType> GetSupportedImages(BaseItem item) => new[] { ImageType.Primary };

    public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, LibraryOptions libraryOptions, CancellationToken cancellationToken)
    {
        var cfg = Plugin.Instance?.Configuration ?? new PluginConfiguration();
        var tmdbId = item.ProviderIds.TryGetValue("Tmdb", out var id) ? id : null;
        if (!string.IsNullOrWhiteSpace(tmdbId))
        {
            var safeTmdbId = tmdbId;
            var data = await _tmdb.GetPersonAsync(safeTmdbId, cfg, cancellationToken).ConfigureAwait(false);
            if (data != null)
            {
                return MetadataProviderFactory.BuildRemoteImages(Name, cfg.TmdbLanguage,
                    (_tmdb.GetImageUrl(data.Profile_Path), _tmdb.GetImageUrl(data.Profile_Path, "w500"), ImageType.Primary)).ToArray();
            }
        }

        var tvdbId = item.ProviderIds.TryGetValue("Tvdb", out var tvdbProviderId) ? tvdbProviderId : null;
        if (!string.IsNullOrWhiteSpace(tvdbId) && cfg.EnableTvdbFallback)
        {
            var safeTvdbId = tvdbId;
            var person = await _tvdb.GetPersonAsync(safeTvdbId, cfg, cancellationToken).ConfigureAwait(false);
            if (person?.Data != null)
            {
                return MetadataProviderFactory.BuildRemoteImages(Name, cfg.TvdbLanguage,
                    (_tvdb.NormalizeImageUrl(person.Data.Image), _tvdb.NormalizeImageUrl(person.Data.Image), ImageType.Primary)).ToArray();
            }
        }

        return Array.Empty<RemoteImageInfo>();
    }

    public Task<HttpResponseInfo> GetImageResponse(string url, CancellationToken cancellationToken) => MetadataProviderFactory.GetImageResponseAsync(url, cancellationToken);
}
