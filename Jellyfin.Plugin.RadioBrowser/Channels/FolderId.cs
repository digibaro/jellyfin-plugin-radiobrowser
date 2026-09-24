using System;

namespace Jellyfin.Plugin.RadioBrowser.Channels;

/// <summary>
/// Builds and parses the external folder ids used by the channel.
/// </summary>
/// <remarks>
/// Jellyfin stores each channel item under exactly one parent folder and removes items that
/// disappear from a folder listing. A station that appears in several folders therefore needs a
/// different item id per folder, see <see cref="StationItemId"/>.
/// </remarks>
public static class FolderId
{
    /// <summary>Admin-curated stations.</summary>
    public const string Featured = "featured";

    /// <summary>Stations of the configured local country.</summary>
    public const string Local = "local";

    /// <summary>Most clicked stations.</summary>
    public const string TopClick = "top-click";

    /// <summary>Most voted stations.</summary>
    public const string TopVote = "top-vote";

    /// <summary>Stations gaining listeners.</summary>
    public const string Trending = "trending";

    /// <summary>List of countries.</summary>
    public const string Countries = "countries";

    /// <summary>List of genres.</summary>
    public const string Genres = "genres";

    /// <summary>List of languages.</summary>
    public const string Languages = "languages";

    /// <summary>Prefix of a single country folder.</summary>
    public const string CountryPrefix = "country:";

    /// <summary>Prefix of a single genre folder.</summary>
    public const string GenrePrefix = "genre:";

    /// <summary>Prefix of a single language folder.</summary>
    public const string LanguagePrefix = "lang:";

    private const char StationSeparator = '|';

    /// <summary>
    /// Builds the item id of a station inside a folder.
    /// </summary>
    /// <param name="folderId">The folder id.</param>
    /// <param name="stationUuid">The station UUID.</param>
    /// <returns>The item id.</returns>
    public static string StationItemId(string folderId, string stationUuid)
        => folderId + StationSeparator + stationUuid;

    /// <summary>
    /// Tries to read a value after a prefix, e.g. "DE" from "country:DE".
    /// </summary>
    /// <param name="folderId">The folder id.</param>
    /// <param name="prefix">The prefix.</param>
    /// <param name="value">The value after the prefix.</param>
    /// <returns><c>true</c> if the folder id has the prefix and a value.</returns>
    public static bool TryGetValue(string folderId, string prefix, out string value)
    {
        ArgumentNullException.ThrowIfNull(folderId);

        if (folderId.StartsWith(prefix, StringComparison.Ordinal) && folderId.Length > prefix.Length)
        {
            value = folderId[prefix.Length..];
            return true;
        }

        value = string.Empty;
        return false;
    }
}
