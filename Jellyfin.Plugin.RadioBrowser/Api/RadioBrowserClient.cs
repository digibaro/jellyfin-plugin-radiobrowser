using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.RadioBrowser.Api;

/// <summary>
/// Minimal client for the radio-browser.info API that follows the operator's usage rules:
/// servers are discovered through DNS, tried in random order, requests carry a descriptive
/// user agent, and plays are reported through /json/url.
/// </summary>
public sealed class RadioBrowserClient : IDisposable
{
    private const string DiscoveryHost = "all.api.radio-browser.info";
    private const int MaxAttempts = 3;

    // Used only when DNS discovery fails completely. Never the primary path.
    private static readonly string[] _fallbackServers =
    [
        "https://de1.api.radio-browser.info",
        "https://de2.api.radio-browser.info",
        "https://fi1.api.radio-browser.info"
    ];

    private static readonly TimeSpan _serverListLifetime = TimeSpan.FromHours(1);
    private static readonly TimeSpan _requestTimeout = TimeSpan.FromSeconds(15);

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        PropertyNameCaseInsensitive = true
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<RadioBrowserClient> _logger;
    private readonly SemaphoreSlim _serverLock = new(1, 1);
    private readonly string _version;

    private IReadOnlyList<string> _servers = [];
    private DateTime _serversFetchedUtc = DateTime.MinValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="RadioBrowserClient"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="logger">The logger.</param>
    public RadioBrowserClient(IHttpClientFactory httpClientFactory, ILogger<RadioBrowserClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _version = typeof(RadioBrowserClient).Assembly.GetName().Version?.ToString() ?? "1.0";
    }

    /// <summary>
    /// Gets all countries, most stations first.
    /// </summary>
    /// <param name="hideBroken">Whether to count only working stations.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The countries.</returns>
    public Task<IReadOnlyList<RadioCategory>> GetCountriesAsync(bool hideBroken, CancellationToken cancellationToken)
        => GetAsync<RadioCategory>("/json/countries?order=stationcount&reverse=true" + HideBroken(hideBroken), cancellationToken);

    /// <summary>
    /// Gets the most used tags.
    /// </summary>
    /// <param name="limit">Maximum number of tags.</param>
    /// <param name="hideBroken">Whether to count only working stations.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The tags.</returns>
    public Task<IReadOnlyList<RadioCategory>> GetTagsAsync(int limit, bool hideBroken, CancellationToken cancellationToken)
        => GetAsync<RadioCategory>(
            "/json/tags?order=stationcount&reverse=true&limit=" + limit.ToString(CultureInfo.InvariantCulture) + HideBroken(hideBroken),
            cancellationToken);

    /// <summary>
    /// Gets all languages, most stations first.
    /// </summary>
    /// <param name="hideBroken">Whether to count only working stations.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The languages.</returns>
    public Task<IReadOnlyList<RadioCategory>> GetLanguagesAsync(bool hideBroken, CancellationToken cancellationToken)
        => GetAsync<RadioCategory>("/json/languages?order=stationcount&reverse=true" + HideBroken(hideBroken), cancellationToken);

    /// <summary>
    /// Searches stations.
    /// </summary>
    /// <param name="query">The filters.</param>
    /// <param name="hideBroken">Whether to omit stations that failed their last check.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The matching stations.</returns>
    public Task<IReadOnlyList<RadioStation>> SearchStationsAsync(StationQuery query, bool hideBroken, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var sb = new StringBuilder("/json/stations/search?limit=");
        sb.Append(Math.Clamp(query.Limit, 1, 100000).ToString(CultureInfo.InvariantCulture));
        sb.Append(query.Sort switch
        {
            StationSort.Votes => "&order=votes&reverse=true",
            StationSort.ClickTrend => "&order=clicktrend&reverse=true",
            StationSort.Name => "&order=name",
            _ => "&order=clickcount&reverse=true"
        });
        AppendParameter(sb, "name", query.Name);
        AppendParameter(sb, "countrycode", query.CountryCode);
        if (!string.IsNullOrWhiteSpace(query.Tag))
        {
            AppendParameter(sb, "tag", query.Tag);
            sb.Append("&tagExact=true");
        }

        if (!string.IsNullOrWhiteSpace(query.Language))
        {
            AppendParameter(sb, "language", query.Language);
            sb.Append("&languageExact=true");
        }

        sb.Append(HideBroken(hideBroken));
        return GetAsync<RadioStation>(sb.ToString(), cancellationToken);
    }

    /// <summary>
    /// Gets stations by their UUIDs, in the requested order.
    /// </summary>
    /// <param name="uuids">The station UUIDs.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The stations that still exist.</returns>
    public async Task<IReadOnlyList<RadioStation>> GetStationsByUuidAsync(IEnumerable<string> uuids, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(uuids);

        var wanted = uuids
            .Where(u => Guid.TryParse(u, out _))
            .Select(u => u.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (wanted.Count == 0)
        {
            return [];
        }

        var result = new List<RadioStation>();

        // Keep URLs short; the endpoint accepts a comma separated list.
        foreach (var chunk in wanted.Chunk(50))
        {
            var stations = await GetAsync<RadioStation>(
                "/json/stations/byuuid?uuids=" + string.Join(',', chunk),
                cancellationToken).ConfigureAwait(false);
            result.AddRange(stations);
        }

        var order = wanted.Select((u, i) => (u, i)).ToDictionary(x => x.u, x => x.i, StringComparer.OrdinalIgnoreCase);
        return result
            .Where(s => order.ContainsKey(s.StationUuid))
            .OrderBy(s => order[s.StationUuid])
            .ToList();
    }

    /// <summary>
    /// Reports that a station was played. Failures are logged and swallowed.
    /// </summary>
    /// <param name="stationUuid">The station UUID.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task ReportClickAsync(string stationUuid, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(stationUuid, out var uuid))
        {
            return;
        }

        try
        {
            var path = "/json/url/" + uuid.ToString("D", CultureInfo.InvariantCulture);
            using var response = await SendAsync(path, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            _logger.LogDebug(ex, "Could not report click for station {StationUuid}", stationUuid);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _serverLock.Dispose();
    }

    private static string HideBroken(bool hideBroken) => hideBroken ? "&hidebroken=true" : string.Empty;

    private static void AppendParameter(StringBuilder sb, string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        sb.Append('&').Append(name).Append('=').Append(Uri.EscapeDataString(value.Trim()));
    }

    private static void Shuffle(List<string> list)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private async Task<IReadOnlyList<T>> GetAsync<T>(string pathAndQuery, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(pathAndQuery, cancellationToken).ConfigureAwait(false);
        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            var items = await JsonSerializer.DeserializeAsync<List<T>>(stream, _jsonOptions, cancellationToken).ConfigureAwait(false);
            return items ?? [];
        }
    }

    /// <summary>
    /// Sends a GET request, trying up to <see cref="MaxAttempts"/> servers in random order.
    /// The caller owns the returned response.
    /// </summary>
    private async Task<HttpResponseMessage> SendAsync(string pathAndQuery, CancellationToken cancellationToken)
    {
        var servers = await GetServersAsync(cancellationToken).ConfigureAwait(false);
        var client = _httpClientFactory.CreateClient(NamedClient.Default);
        Exception? lastError = null;

        foreach (var server in servers.Take(MaxAttempts))
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(server + pathAndQuery));
            request.Headers.UserAgent.Clear();
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("Jellyfin-RadioBrowser", _version));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(_requestTimeout);

            HttpResponseMessage? response = null;
            try
            {
                response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                return response;
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested
                && ex is HttpRequestException or TaskCanceledException)
            {
                response?.Dispose();
                lastError = ex;
                _logger.LogWarning("Radio Browser server {Server} failed ({Message}), trying next server", server, ex.Message);
                InvalidateServers();
            }
        }

        throw new HttpRequestException("All Radio Browser API servers failed.", lastError);
    }

    private void InvalidateServers()
    {
        // Force a fresh discovery and a new random order on the next request.
        _serversFetchedUtc = DateTime.MinValue;
    }

    private async Task<IReadOnlyList<string>> GetServersAsync(CancellationToken cancellationToken)
    {
        var configured = Plugin.CurrentConfiguration.ApiServerOverride?.Trim().TrimEnd('/');
        if (!string.IsNullOrEmpty(configured))
        {
            return [configured];
        }

        if (_servers.Count > 0 && DateTime.UtcNow - _serversFetchedUtc < _serverListLifetime)
        {
            return _servers;
        }

        await _serverLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_servers.Count > 0 && DateTime.UtcNow - _serversFetchedUtc < _serverListLifetime)
            {
                return _servers;
            }

            var discovered = await DiscoverServersAsync(cancellationToken).ConfigureAwait(false);
            var list = discovered.Count > 0 ? discovered : _fallbackServers.ToList();
            Shuffle(list);
            _servers = list;
            _serversFetchedUtc = DateTime.UtcNow;
            _logger.LogDebug("Using Radio Browser servers: {Servers}", string.Join(", ", list));
            return _servers;
        }
        finally
        {
            _serverLock.Release();
        }
    }

    /// <summary>
    /// DNS lookup of all.api.radio-browser.info followed by reverse lookups to get the
    /// host names, which are required for valid HTTPS certificates.
    /// </summary>
    private async Task<List<string>> DiscoverServersAsync(CancellationToken cancellationToken)
    {
        var hosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var addresses = await Dns.GetHostAddressesAsync(DiscoveryHost, cancellationToken).ConfigureAwait(false);
            foreach (var address in addresses)
            {
                try
                {
                    var entry = await Dns.GetHostEntryAsync(address.ToString(), cancellationToken).ConfigureAwait(false);
                    if (!string.IsNullOrEmpty(entry.HostName)
                        && entry.HostName.EndsWith(".api.radio-browser.info", StringComparison.OrdinalIgnoreCase))
                    {
                        hosts.Add(entry.HostName.TrimEnd('.'));
                    }
                }
                catch (System.Net.Sockets.SocketException ex)
                {
                    _logger.LogDebug(ex, "Reverse DNS lookup failed for {Address}", address);
                }
            }
        }
        catch (System.Net.Sockets.SocketException ex)
        {
            _logger.LogWarning(ex, "DNS discovery of Radio Browser servers failed, using fallback list");
        }

        return hosts.Select(h => "https://" + h).ToList();
    }
}
