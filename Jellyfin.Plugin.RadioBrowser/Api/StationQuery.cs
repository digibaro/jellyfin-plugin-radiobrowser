namespace Jellyfin.Plugin.RadioBrowser.Api;

/// <summary>
/// Filters for a station search. Unset properties are not sent.
/// </summary>
public class StationQuery
{
    /// <summary>Gets or sets a name substring.</summary>
    public string? Name { get; set; }

    /// <summary>Gets or sets the exact ISO 3166-1 country code.</summary>
    public string? CountryCode { get; set; }

    /// <summary>Gets or sets an exact tag.</summary>
    public string? Tag { get; set; }

    /// <summary>Gets or sets an exact language.</summary>
    public string? Language { get; set; }

    /// <summary>Gets or sets the ordering.</summary>
    public StationSort Sort { get; set; } = StationSort.ClickCount;

    /// <summary>Gets or sets the maximum number of results.</summary>
    public int Limit { get; set; } = 100;
}
