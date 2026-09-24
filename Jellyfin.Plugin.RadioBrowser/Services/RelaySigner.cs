using System;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Jellyfin.Plugin.RadioBrowser.Services;

/// <summary>
/// Signs stream URLs so the anonymous relay endpoint only forwards streams this plugin handed out,
/// and cannot be abused as an open proxy.
/// </summary>
public static class RelaySigner
{
    /// <summary>
    /// Builds a signed relay URL for a stream.
    /// </summary>
    /// <param name="relayBaseUrl">The public address of the Jellyfin server.</param>
    /// <param name="streamUrl">The station stream URL.</param>
    /// <param name="secret">The signing secret.</param>
    /// <returns>The relay URL.</returns>
    public static string BuildRelayUrl(string relayBaseUrl, string streamUrl, string secret)
    {
        ArgumentNullException.ThrowIfNull(relayBaseUrl);
        ArgumentNullException.ThrowIfNull(streamUrl);

        var encoded = Base64Url.EncodeToString(Encoding.UTF8.GetBytes(streamUrl));
        return relayBaseUrl.Trim().TrimEnd('/')
            + "/RadioBrowser/Relay?u=" + encoded
            + "&s=" + Sign(encoded, secret);
    }

    /// <summary>
    /// Verifies a relay request and decodes the stream URL.
    /// </summary>
    /// <param name="encodedUrl">The encoded stream URL.</param>
    /// <param name="signature">The signature.</param>
    /// <param name="secret">The signing secret.</param>
    /// <param name="streamUri">The decoded stream URI.</param>
    /// <returns><c>true</c> if the signature is valid and the URL is an http(s) URL.</returns>
    public static bool TryVerify(string encodedUrl, string signature, string secret, out Uri? streamUri)
    {
        streamUri = null;
        if (string.IsNullOrEmpty(encodedUrl) || string.IsNullOrEmpty(signature) || string.IsNullOrEmpty(secret))
        {
            return false;
        }

        var expected = Encoding.ASCII.GetBytes(Sign(encodedUrl, secret));
        var actual = Encoding.ASCII.GetBytes(signature);
        if (!CryptographicOperations.FixedTimeEquals(expected, actual))
        {
            return false;
        }

        string url;
        try
        {
            url = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(encodedUrl));
        }
        catch (FormatException)
        {
            return false;
        }

        return Uri.TryCreate(url, UriKind.Absolute, out streamUri)
            && (streamUri.Scheme == Uri.UriSchemeHttp || streamUri.Scheme == Uri.UriSchemeHttps);
    }

    private static string Sign(string value, string secret)
    {
        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(value));
        return Base64Url.EncodeToString(mac);
    }
}
