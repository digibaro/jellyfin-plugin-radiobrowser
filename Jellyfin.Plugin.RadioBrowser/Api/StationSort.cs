namespace Jellyfin.Plugin.RadioBrowser.Api;

/// <summary>
/// Station orderings supported by the search endpoint.
/// </summary>
public enum StationSort
{
    /// <summary>Most clicked in the last 24 hours.</summary>
    ClickCount = 0,

    /// <summary>Most votes.</summary>
    Votes = 1,

    /// <summary>Largest increase in clicks.</summary>
    ClickTrend = 2,

    /// <summary>Alphabetical.</summary>
    Name = 3
}
