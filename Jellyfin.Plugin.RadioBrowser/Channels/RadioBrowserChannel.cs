using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.RadioBrowser.Api;
using Jellyfin.Plugin.RadioBrowser.Configuration;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Channels;
using MediaBrowser.Model.Drawing;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.RadioBrowser.Channels;

/// <summary>
/// The "Internet Radio" channel. Jellyfin lists every channel as its own top-level entry next to
/// Live TV, which gives listeners a dedicated place to browse and play stations.
/// </summary>
public class RadioBrowserChannel : IChannel, IHasCacheKey
{
    /// <summary>
    /// The channel name. Jellyfin derives the channel's internal id from it.
    /// </summary>
    public const string ChannelName = "Internet Radio";

    private readonly RadioBrowserClient _client;
    private readonly IServerConfigurationManager _serverConfig;
    private readonly ILogger<RadioBrowserChannel> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RadioBrowserChannel"/> class.
    /// </summary>
    /// <param name="client">The Radio Browser client.</param>
    /// <param name="serverConfig">The server configuration, used for the display language.</param>
    /// <param name="logger">The logger.</param>
    public RadioBrowserChannel(RadioBrowserClient client, IServerConfigurationManager serverConfig, ILogger<RadioBrowserChannel> logger)
    {
        _client = client;
        _serverConfig = serverConfig;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => ChannelName;

    /// <inheritdoc />
    public string Description => "Internet radio stations from the community directory radio-browser.info.";

    /// <inheritdoc />
    public string DataVersion => "3";

    /// <inheritdoc />
    public string HomePageUrl => "https://www.radio-browser.info";

    /// <inheritdoc />
    public ChannelParentalRating ParentalRating => ChannelParentalRating.GeneralAudience;

    private string Culture => _serverConfig.Configuration.UICulture;

    /// <inheritdoc />
    public InternalChannelFeatures GetChannelFeatures()
    {
        return new InternalChannelFeatures
        {
            MediaTypes = [ChannelMediaType.Audio],
            ContentTypes = [ChannelMediaContentType.Song],
            SupportsContentDownloading = false
        };
    }

    /// <inheritdoc />
    public bool IsEnabledFor(string userId) => true;

    /// <inheritdoc />
    public string? GetCacheKey(string? userId) => Plugin.CurrentConfiguration.GetListingCacheKey() + "|" + Culture;

    /// <inheritdoc />
    public async Task<ChannelItemResult> GetChannelItems(InternalChannelItemQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var config = Plugin.CurrentConfiguration;
        var folderId = query.FolderId ?? string.Empty;

        try
        {
            var items = await GetItemsAsync(folderId, config, cancellationToken).ConfigureAwait(false);
            return new ChannelItemResult { Items = items, TotalRecordCount = items.Count };
        }
        catch (HttpRequestException ex)
        {
            // Rethrow instead of returning an empty list: Jellyfin caches channel results for
            // hours, and an empty cached folder would hide stations after a short API outage.
            _logger.LogError(ex, "Loading Internet Radio folder '{FolderId}' failed", folderId);
            throw;
        }
    }

    /// <inheritdoc />
    public Task<DynamicImageResponse> GetChannelImage(ImageType type, CancellationToken cancellationToken)
    {
        // Thumb (16:9, icon and name) is what the home screen tiles use; Primary is the square icon.
        var file = type == ImageType.Primary ? ".Images.channel.png" : ".Images.thumb.png";
        var resource = typeof(RadioBrowserChannel).Namespace!.Replace(".Channels", file, StringComparison.Ordinal);
        var stream = typeof(RadioBrowserChannel).Assembly.GetManifestResourceStream(resource);

        return Task.FromResult(new DynamicImageResponse
        {
            Format = ImageFormat.Png,
            HasImage = stream is not null,
            Stream = stream
        });
    }

    /// <inheritdoc />
    public IEnumerable<ImageType> GetSupportedChannelImages() => [ImageType.Primary, ImageType.Thumb, ImageType.Backdrop];

    /// <summary>
    /// A folder listing other folders. Always a plain container.
    /// </summary>
    private static ChannelItemInfo Folder(string id, string name, string? overview = null)
    {
        return new ChannelItemInfo
        {
            Id = id,
            Name = name,
            Overview = overview,
            Type = ChannelItemType.Folder,
            FolderType = ChannelFolderType.Container
        };
    }

    /// <summary>
    /// A folder listing stations. As a music album it opens on the music-style details page.
    /// </summary>
    private static ChannelItemInfo StationFolder(string id, string name, PluginConfiguration config, string? overview = null)
    {
        var folder = Folder(id, name, overview);
        if (config.ShowStationListsAsAlbums)
        {
            folder.FolderType = ChannelFolderType.MusicAlbum;
        }

        return folder;
    }

    private static string StationCount(int count, string culture)
        => string.Format(CultureInfo.InvariantCulture, FolderText.Get(count == 1 ? "Station" : "Stations", culture), count.ToString("N0", CultureInfo.InvariantCulture));

    private static List<ChannelItemInfo> GetRootFolders(PluginConfiguration config, string culture)
    {
        string T(string key) => FolderText.Get(key, culture);
        var folders = new List<ChannelItemInfo>();

        if (config.FeaturedStationUuids is { Length: > 0 })
        {
            folders.Add(StationFolder(FolderId.Featured, T("Favorites"), config, T("FavoritesInfo")));
        }

        if (!string.IsNullOrWhiteSpace(config.LocalCountryCode))
        {
            var code = config.LocalCountryCode.Trim().ToUpperInvariant();
            folders.Add(StationFolder(FolderId.Local, T("Local"), config, string.Format(CultureInfo.InvariantCulture, T("LocalInfo"), code)));
        }

        folders.Add(StationFolder(FolderId.TopClick, T("MostPlayed"), config, T("MostPlayedInfo")));
        folders.Add(StationFolder(FolderId.TopVote, T("TopVoted"), config, T("TopVotedInfo")));
        folders.Add(StationFolder(FolderId.Trending, T("Trending"), config, T("TrendingInfo")));
        folders.Add(Folder(FolderId.Countries, T("ByCountry")));
        folders.Add(Folder(FolderId.Genres, T("ByGenre")));
        folders.Add(Folder(FolderId.Languages, T("ByLanguage")));

        return folders;
    }

    private static List<ChannelItemInfo> ToItems(IEnumerable<RadioStation> stations, string folderId, PluginConfiguration config)
    {
        return stations
            .Where(s => !config.ExcludeHls || !s.IsHls)
            .DistinctBy(s => s.StationUuid, StringComparer.OrdinalIgnoreCase)
            .Select(s => StationMapper.ToChannelItem(s, folderId, config))
            .OfType<ChannelItemInfo>()
            .ToList();
    }

    private async Task<IReadOnlyList<ChannelItemInfo>> GetItemsAsync(string folderId, PluginConfiguration config, CancellationToken cancellationToken)
    {
        var limit = Math.Clamp(config.MaxStationsPerFolder, 10, 2000);
        var hideBroken = config.HideBrokenStations;

        switch (folderId)
        {
            case "":
                return GetRootFolders(config, Culture);

            case FolderId.Featured:
                var featured = await _client.GetStationsByUuidAsync(config.FeaturedStationUuids ?? [], cancellationToken).ConfigureAwait(false);
                return ToItems(featured, folderId, config);

            case FolderId.Local:
                return await SearchAsync(folderId, new StationQuery { CountryCode = config.LocalCountryCode, Limit = limit }, config, cancellationToken).ConfigureAwait(false);

            case FolderId.TopClick:
                return await SearchAsync(folderId, new StationQuery { Sort = StationSort.ClickCount, Limit = limit }, config, cancellationToken).ConfigureAwait(false);

            case FolderId.TopVote:
                return await SearchAsync(folderId, new StationQuery { Sort = StationSort.Votes, Limit = limit }, config, cancellationToken).ConfigureAwait(false);

            case FolderId.Trending:
                return await SearchAsync(folderId, new StationQuery { Sort = StationSort.ClickTrend, Limit = limit }, config, cancellationToken).ConfigureAwait(false);

            case FolderId.Countries:
                var countries = await _client.GetCountriesAsync(hideBroken, cancellationToken).ConfigureAwait(false);
                return countries
                    .Where(c => !string.IsNullOrWhiteSpace(c.CountryCode) && c.StationCount >= config.MinStationsPerCategory)
                    .OrderBy(c => c.Name, StringComparer.CurrentCultureIgnoreCase)
                    .Select(c => StationFolder(FolderId.CountryPrefix + c.CountryCode!.ToUpperInvariant(), c.Name, config, StationCount(c.StationCount, Culture)))
                    .ToList();

            case FolderId.Genres:
                var tags = await _client.GetTagsAsync(Math.Clamp(config.MaxGenres, 10, 1000), hideBroken, cancellationToken).ConfigureAwait(false);
                return tags
                    .Where(t => !string.IsNullOrWhiteSpace(t.Name) && t.StationCount >= config.MinStationsPerCategory)
                    .OrderBy(t => t.Name, StringComparer.CurrentCultureIgnoreCase)
                    .Select(t => StationFolder(FolderId.GenrePrefix + t.Name, StationMapper.ToDisplayName(t.Name), config, StationCount(t.StationCount, Culture)))
                    .ToList();

            case FolderId.Languages:
                var languages = await _client.GetLanguagesAsync(hideBroken, cancellationToken).ConfigureAwait(false);
                return languages
                    .Where(l => !string.IsNullOrWhiteSpace(l.Name) && l.StationCount >= config.MinStationsPerCategory)
                    .OrderBy(l => l.Name, StringComparer.CurrentCultureIgnoreCase)
                    .Select(l => StationFolder(FolderId.LanguagePrefix + l.Name, StationMapper.ToDisplayName(l.Name), config, StationCount(l.StationCount, Culture)))
                    .ToList();
        }

        if (FolderId.TryGetValue(folderId, FolderId.CountryPrefix, out var countryCode))
        {
            return await SearchAsync(folderId, new StationQuery { CountryCode = countryCode, Limit = limit }, config, cancellationToken).ConfigureAwait(false);
        }

        if (FolderId.TryGetValue(folderId, FolderId.GenrePrefix, out var tag))
        {
            return await SearchAsync(folderId, new StationQuery { Tag = tag, Limit = limit }, config, cancellationToken).ConfigureAwait(false);
        }

        if (FolderId.TryGetValue(folderId, FolderId.LanguagePrefix, out var language))
        {
            return await SearchAsync(folderId, new StationQuery { Language = language, Limit = limit }, config, cancellationToken).ConfigureAwait(false);
        }

        _logger.LogWarning("Unknown Internet Radio folder '{FolderId}'", folderId);
        return [];
    }

    private async Task<IReadOnlyList<ChannelItemInfo>> SearchAsync(string folderId, StationQuery query, PluginConfiguration config, CancellationToken cancellationToken)
    {
        var stations = await _client.SearchStationsAsync(query, config.HideBrokenStations, cancellationToken).ConfigureAwait(false);
        return ToItems(stations, folderId, config);
    }
}
