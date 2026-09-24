using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Jellyfin.Plugin.RadioBrowser.Api;
using Jellyfin.Plugin.RadioBrowser.Configuration;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Model.Channels;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.MediaInfo;

namespace Jellyfin.Plugin.RadioBrowser.Channels;

/// <summary>
/// Converts Radio Browser stations into Jellyfin channel items.
/// </summary>
public static class StationMapper
{
    /// <summary>
    /// Provider id key holding the Radio Browser station UUID on every station item.
    /// </summary>
    public const string ProviderKey = "RadioBrowser";

    /// <summary>
    /// Release date of the current stream mapping. Items stored by older versions are older than this,
    /// which makes Jellyfin rewrite their saved stream information.
    /// </summary>
    private static readonly DateTime _mappingEpochUtc = new(2026, 9, 24, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Maps a station to a playable channel item.
    /// </summary>
    /// <param name="station">The station.</param>
    /// <param name="folderId">The folder the item is listed in.</param>
    /// <param name="config">The plugin configuration.</param>
    /// <returns>The channel item, or <c>null</c> if the station has no usable stream URL.</returns>
    public static ChannelItemInfo? ToChannelItem(RadioStation station, string folderId, PluginConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(station);
        ArgumentNullException.ThrowIfNull(config);

        var url = station.StreamUrl?.Trim();
        if (string.IsNullOrEmpty(url)
            || !Uri.TryCreate(url, UriKind.Absolute, out var streamUri)
            || (streamUri.Scheme != Uri.UriSchemeHttp && streamUri.Scheme != Uri.UriSchemeHttps)
            || !Guid.TryParse(station.StationUuid, out var uuid))
        {
            return null;
        }

        var name = string.IsNullOrWhiteSpace(station.Name) ? url : station.Name.Trim();
        var (codec, container) = MapCodec(station.Codec, streamUri, station.IsHls);

        var item = new ChannelItemInfo
        {
            Id = FolderId.StationItemId(folderId, station.StationUuid),
            Name = name,
            Type = ChannelItemType.Media,
            MediaType = ChannelMediaType.Audio,
            ContentType = ChannelMediaContentType.Song,
            IsLiveStream = true,
            Overview = BuildOverview(station),
            HomePageUrl = station.Homepage,
            ImageUrl = IsHttpUrl(station.Favicon) ? station.Favicon : null,
            Genres = SplitList(station.Tags).Take(6).ToList(),
            DateModified = Max(ParseDate(station.LastChangeTimeIso8601), _mappingEpochUtc, config.SettingsChangedUtc)
        };
        item.ProviderIds[ProviderKey] = station.StationUuid;

        var bitrate = station.Bitrate > 0 ? station.Bitrate * 1000 : (int?)null;
        item.MediaSources.Add(new MediaSourceInfo
        {
            // Must NOT parse as a Guid: Jellyfin drops static sources whose Guid id is not a library item.
            Id = "rb_" + uuid.ToString("N", CultureInfo.InvariantCulture),
            Name = name,
            // The station's own link. Jellyfin apps fetch it through the server
            // (Audio/{id}/universal), so browsers never load plain-HTTP audio themselves.
            Path = url,
            Protocol = MediaProtocol.Http,
            Type = MediaSourceType.Default,
            IsRemote = true,
            IsInfiniteStream = true,
            Container = container,
            Bitrate = bitrate,

            // Always direct play. Jellyfin 12 cannot transcode live audio for browsers: its HLS
            // playlist points to Audio/{id}/live.m3u8, which does not exist, and the web client
            // reports "fatal player error".
            SupportsDirectPlay = true,
            SupportsDirectStream = false,
            SupportsTranscoding = false,

            // Declaring the audio stream up front stops Jellyfin from ffprobing an endless stream.
            MediaStreams =
            [
                new MediaStream
                {
                    Type = MediaStreamType.Audio,
                    Index = -1,
                    Codec = codec,
                    BitRate = bitrate,
                    IsDefault = true
                }
            ]
        });

        return item;
    }

    /// <summary>
    /// Splits a comma separated Radio Browser list.
    /// </summary>
    /// <param name="value">The list.</param>
    /// <returns>The trimmed, distinct entries.</returns>
    public static IEnumerable<string> SplitList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Makes a lower-case tag or language readable, e.g. "classic rock" becomes "Classic Rock".
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The display name.</returns>
    public static string ToDisplayName(string value)
        => CultureInfo.InvariantCulture.TextInfo.ToTitleCase((value ?? string.Empty).Trim());

    private static DateTime Max(DateTime a, DateTime b, DateTime c)
    {
        var max = a > b ? a : b;
        return max > c ? max : c;
    }

    private static (string? Codec, string? Container) MapCodec(string? codec, Uri streamUri, bool isHls)
    {
        var c = (codec ?? string.Empty).ToUpperInvariant();
        if (c.Contains("AAC", StringComparison.Ordinal))
        {
            return ("aac", "aac");
        }

        if (c.Contains("MP3", StringComparison.Ordinal))
        {
            return ("mp3", "mp3");
        }

        if (c.Contains("OPUS", StringComparison.Ordinal))
        {
            return ("opus", "ogg");
        }

        if (c.Contains("OGG", StringComparison.Ordinal) || c.Contains("VORBIS", StringComparison.Ordinal))
        {
            return ("vorbis", "ogg");
        }

        if (c.Contains("FLAC", StringComparison.Ordinal))
        {
            return ("flac", "flac");
        }

        // Unknown codec: guess from the URL. A container is required for Jellyfin to allow
        // direct play; players detect the real format from the stream itself.
        var path = streamUri.AbsolutePath;
        if (isHls || path.EndsWith(".aac", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".m4a", StringComparison.OrdinalIgnoreCase))
        {
            return (null, "aac");
        }

        if (path.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".oga", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".opus", StringComparison.OrdinalIgnoreCase))
        {
            return (null, "ogg");
        }

        if (path.EndsWith(".flac", StringComparison.OrdinalIgnoreCase))
        {
            return (null, "flac");
        }

        return (null, "mp3");
    }

    private static string BuildOverview(RadioStation station)
    {
        var sb = new StringBuilder();

        var place = string.Join(", ", new[] { station.State, station.Country }.Where(s => !string.IsNullOrWhiteSpace(s)));
        if (place.Length > 0)
        {
            sb.AppendLine(place);
        }

        var languages = SplitList(station.Language).Select(ToDisplayName).ToList();
        if (languages.Count > 0)
        {
            sb.Append("Language: ").AppendLine(string.Join(", ", languages));
        }

        if (!string.IsNullOrWhiteSpace(station.Codec) && !station.Codec.Equals("UNKNOWN", StringComparison.OrdinalIgnoreCase))
        {
            sb.Append(station.Codec);
            if (station.Bitrate > 0)
            {
                sb.Append(CultureInfo.InvariantCulture, $", {station.Bitrate} kbit/s");
            }

            sb.AppendLine();
        }

        if (IsHttpUrl(station.Homepage))
        {
            sb.AppendLine(station.Homepage);
        }

        return sb.ToString().TrimEnd();
    }

    private static bool IsHttpUrl(string? value)
        => !string.IsNullOrWhiteSpace(value)
            && Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static DateTime ParseDate(string? iso)
    {
        return DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var date)
            ? date
            : DateTime.MinValue;
    }
}
