using System;
using Jellyfin.Plugin.RadioBrowser.Api;

namespace Jellyfin.Plugin.RadioBrowser.Controllers;

/// <summary>
/// Compact station data for the configuration page.
/// </summary>
/// <param name="Uuid">The station UUID.</param>
/// <param name="Name">The station name.</param>
/// <param name="Country">The country code.</param>
/// <param name="Codec">The codec.</param>
/// <param name="Bitrate">The bitrate in kbit/s.</param>
/// <param name="Favicon">The logo URL.</param>
public record StationSummary(string Uuid, string Name, string? Country, string? Codec, int Bitrate, string? Favicon)
{
    /// <summary>
    /// Creates a summary from a station.
    /// </summary>
    /// <param name="station">The station.</param>
    /// <returns>The summary.</returns>
    public static StationSummary From(RadioStation station)
    {
        ArgumentNullException.ThrowIfNull(station);
        return new StationSummary(station.StationUuid, station.Name.Trim(), station.CountryCode, station.Codec, station.Bitrate, station.Favicon);
    }
}
