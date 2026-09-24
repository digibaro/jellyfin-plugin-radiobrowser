using System;
using System.Globalization;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.RadioBrowser.Configuration;

/// <summary>
/// Plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets an optional fixed API server, e.g. https://de1.api.radio-browser.info.
    /// Leave empty to use DNS discovery as recommended by radio-browser.info.
    /// </summary>
    public string ApiServerOverride { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the ISO 3166-1 alpha-2 code used for the "Local stations" folder. Empty hides the folder.
    /// </summary>
    public string LocalCountryCode { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the maximum number of stations listed in one folder.
    /// </summary>
    public int MaxStationsPerFolder { get; set; } = 300;

    /// <summary>
    /// Gets or sets the minimum number of stations a country or language needs to be listed.
    /// </summary>
    public int MinStationsPerCategory { get; set; } = 5;

    /// <summary>
    /// Gets or sets the number of genres (tags) listed, most popular first.
    /// </summary>
    public int MaxGenres { get; set; } = 150;

    /// <summary>
    /// Gets or sets a value indicating whether station lists open as music album pages
    /// (track list with Play and Shuffle) instead of the generic folder page, whose filter
    /// menu is fixed by the web client and aimed at video.
    /// </summary>
    public bool ShowStationListsAsAlbums { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether stations that failed their last online check are hidden.
    /// </summary>
    public bool HideBrokenStations { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether HLS stations are hidden.
    /// </summary>
    public bool ExcludeHls { get; set; }

    /// <summary>
    /// Gets or sets the secret used to sign relay URLs. Generated automatically.
    /// Only needed so station links stored by versions 1.0.1–1.0.3 keep working.
    /// </summary>
    public string RelaySecret { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the time the settings were last saved. Used to refresh stored stream information.
    /// </summary>
    public DateTime SettingsChangedUtc { get; set; } = DateTime.MinValue;

    /// <summary>
    /// Gets or sets a value indicating whether playback is reported to radio-browser.info
    /// (requested by the API operator to keep popularity data useful).
    /// </summary>
    public bool ReportClicks { get; set; } = true;

    /// <summary>
    /// Gets or sets the station UUIDs shown in the "Featured" folder, in display order.
    /// </summary>
#pragma warning disable CA1819 // Arrays are required for Jellyfin's XML and JSON configuration round-trip.
    public string[] FeaturedStationUuids { get; set; } = [];
#pragma warning restore CA1819

    /// <summary>
    /// Builds a key that changes whenever a setting affecting channel listings changes,
    /// so Jellyfin's channel cache is invalidated.
    /// </summary>
    /// <returns>The cache key.</returns>
    public string GetListingCacheKey()
    {
        return string.Join(
            '|',
            (LocalCountryCode ?? string.Empty).ToUpperInvariant(),
            MaxStationsPerFolder.ToString(CultureInfo.InvariantCulture),
            MinStationsPerCategory.ToString(CultureInfo.InvariantCulture),
            MaxGenres.ToString(CultureInfo.InvariantCulture),
            HideBrokenStations ? "1" : "0",
            ExcludeHls ? "1" : "0",
            ShowStationListsAsAlbums ? "1" : "0",
            SettingsChangedUtc.Ticks.ToString(CultureInfo.InvariantCulture),
            string.Join(',', FeaturedStationUuids ?? Array.Empty<string>()));
    }
}
