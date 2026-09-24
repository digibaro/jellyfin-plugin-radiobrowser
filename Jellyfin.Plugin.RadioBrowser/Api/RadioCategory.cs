using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.RadioBrowser.Api;

/// <summary>
/// A country, tag or language entry with its station count.
/// </summary>
public class RadioCategory
{
    /// <summary>Gets or sets the display name (also the query value for tags and languages).</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the ISO 3166-1 code (countries only).</summary>
    [JsonPropertyName("iso_3166_1")]
    public string? CountryCode { get; set; }

    /// <summary>Gets or sets the number of stations.</summary>
    [JsonPropertyName("stationcount")]
    public int StationCount { get; set; }
}
