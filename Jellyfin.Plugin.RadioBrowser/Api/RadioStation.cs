using System;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.RadioBrowser.Api;

/// <summary>
/// A station as returned by the radio-browser.info API.
/// </summary>
public class RadioStation
{
    /// <summary>Gets or sets the stable station id, consistent across all API servers.</summary>
    [JsonPropertyName("stationuuid")]
    public string StationUuid { get; set; } = string.Empty;

    /// <summary>Gets or sets the station name.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the stream URL as submitted (may be a playlist).</summary>
    [JsonPropertyName("url")]
    public string? Url { get; set; }

    /// <summary>Gets or sets the stream URL with playlists already resolved.</summary>
    [JsonPropertyName("url_resolved")]
    public string? UrlResolved { get; set; }

    /// <summary>Gets or sets the station homepage.</summary>
    [JsonPropertyName("homepage")]
    public string? Homepage { get; set; }

    /// <summary>Gets or sets the station logo URL.</summary>
    [JsonPropertyName("favicon")]
    public string? Favicon { get; set; }

    /// <summary>Gets or sets the comma separated tags.</summary>
    [JsonPropertyName("tags")]
    public string? Tags { get; set; }

    /// <summary>Gets or sets the country name.</summary>
    [JsonPropertyName("country")]
    public string? Country { get; set; }

    /// <summary>Gets or sets the ISO 3166-1 alpha-2 country code.</summary>
    [JsonPropertyName("countrycode")]
    public string? CountryCode { get; set; }

    /// <summary>Gets or sets the state or region.</summary>
    [JsonPropertyName("state")]
    public string? State { get; set; }

    /// <summary>Gets or sets the comma separated languages.</summary>
    [JsonPropertyName("language")]
    public string? Language { get; set; }

    /// <summary>Gets or sets the number of votes.</summary>
    [JsonPropertyName("votes")]
    public int Votes { get; set; }

    /// <summary>Gets or sets the codec seen at the last check, e.g. MP3, AAC, AAC+, OGG, FLAC.</summary>
    [JsonPropertyName("codec")]
    public string? Codec { get; set; }

    /// <summary>Gets or sets the bitrate in kbit/s seen at the last check.</summary>
    [JsonPropertyName("bitrate")]
    public int Bitrate { get; set; }

    /// <summary>Gets or sets 1 if the stream uses HLS.</summary>
    [JsonPropertyName("hls")]
    public int Hls { get; set; }

    /// <summary>Gets or sets 1 if the last online check succeeded.</summary>
    [JsonPropertyName("lastcheckok")]
    public int LastCheckOk { get; set; }

    /// <summary>Gets or sets the time of the last change to the station record (ISO 8601).</summary>
    [JsonPropertyName("lastchangetime_iso8601")]
    public string? LastChangeTimeIso8601 { get; set; }

    /// <summary>Gets or sets the clicks in the last 24 hours.</summary>
    [JsonPropertyName("clickcount")]
    public int ClickCount { get; set; }

    /// <summary>Gets or sets the change in clicks compared to the day before.</summary>
    [JsonPropertyName("clicktrend")]
    public int ClickTrend { get; set; }

    /// <summary>Gets a value indicating whether the stream uses HLS.</summary>
    [JsonIgnore]
    public bool IsHls => Hls == 1;

    /// <summary>
    /// Gets the best URL to play.
    /// </summary>
    /// <remarks>
    /// <c>url_resolved</c> is a snapshot taken by the directory's checker. For redirecting services
    /// (e.g. StreamTheWorld, used by 538, Qmusic, Sky Radio) that snapshot points to one edge server
    /// that may no longer serve the stream, so the submitted URL is preferred unless it is a playlist
    /// file (which players cannot play) or it would downgrade HTTPS to HTTP.
    /// </remarks>
    [JsonIgnore]
    public string? StreamUrl
    {
        get
        {
            if (string.IsNullOrWhiteSpace(UrlResolved))
            {
                return Url;
            }

            if (string.IsNullOrWhiteSpace(Url) || IsPlaylistFile(Url))
            {
                return UrlResolved;
            }

            var originalIsHttp = Url.StartsWith("http://", StringComparison.OrdinalIgnoreCase);
            var resolvedIsHttps = UrlResolved.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
            return originalIsHttp && resolvedIsHttps ? UrlResolved : Url;
        }
    }

    private static bool IsPlaylistFile(string url)
    {
        var lower = url.ToLowerInvariant();
        return lower.Contains(".pls", StringComparison.Ordinal)
            || lower.Contains(".asx", StringComparison.Ordinal)
            || lower.Contains(".xspf", StringComparison.Ordinal)
            || lower.Contains(".wax", StringComparison.Ordinal)
            || lower.Contains(".ram", StringComparison.Ordinal)
            || (lower.Contains(".m3u", StringComparison.Ordinal) && !lower.Contains(".m3u8", StringComparison.Ordinal));
    }
}
