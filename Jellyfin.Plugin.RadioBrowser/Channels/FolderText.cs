using System;
using System.Collections.Generic;

namespace Jellyfin.Plugin.RadioBrowser.Channels;

/// <summary>
/// Folder names and descriptions in the server's display language (English, Dutch, German).
/// </summary>
public static class FolderText
{
    private static readonly Dictionary<string, string[]> _text = new(StringComparer.Ordinal)
    {
        // key:            English, Dutch, German
        ["Favorites"] = ["Favorites", "Favorieten", "Favoriten"],
        ["FavoritesInfo"] = ["Stations chosen in the plugin settings.", "Zenders gekozen in de plug-in-instellingen.", "In den Plugin-Einstellungen gewählte Sender."],
        ["Local"] = ["Local stations", "Lokale zenders", "Lokale Sender"],
        ["LocalInfo"] = ["Stations from {0}.", "Zenders uit {0}.", "Sender aus {0}."],
        ["MostPlayed"] = ["Most played", "Meest beluisterd", "Meistgehört"],
        ["MostPlayedInfo"] = ["Stations with the most listeners today.", "Zenders met de meeste luisteraars vandaag.", "Sender mit den meisten Hörern heute."],
        ["TopVoted"] = ["Top voted", "Best beoordeeld", "Bestbewertet"],
        ["TopVotedInfo"] = ["Stations with the most votes.", "Zenders met de meeste stemmen.", "Sender mit den meisten Stimmen."],
        ["Trending"] = ["Trending", "Populair nu", "Im Trend"],
        ["TrendingInfo"] = ["Stations gaining listeners.", "Zenders die luisteraars winnen.", "Sender, die Hörer gewinnen."],
        ["ByCountry"] = ["By country", "Per land", "Nach Land"],
        ["ByGenre"] = ["By genre", "Per genre", "Nach Genre"],
        ["ByLanguage"] = ["By language", "Per taal", "Nach Sprache"],
        ["Station"] = ["{0} station", "{0} zender", "{0} Sender"],
        ["Stations"] = ["{0} stations", "{0} zenders", "{0} Sender"]
    };

    /// <summary>
    /// Gets a text for a culture such as "nl-NL"; unknown cultures fall back to English.
    /// </summary>
    /// <param name="key">The text key.</param>
    /// <param name="culture">The server display culture.</param>
    /// <returns>The text.</returns>
    public static string Get(string key, string? culture)
    {
        var index = (culture ?? string.Empty).ToLowerInvariant() switch
        {
            var c when c.StartsWith("nl", StringComparison.Ordinal) => 1,
            var c when c.StartsWith("de", StringComparison.Ordinal) => 2,
            _ => 0
        };

        return _text.TryGetValue(key, out var values) ? values[index] : key;
    }
}
