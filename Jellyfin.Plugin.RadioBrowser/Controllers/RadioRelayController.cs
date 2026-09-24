using System;
using System.ComponentModel.DataAnnotations;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Jellyfin.Plugin.RadioBrowser.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.RadioBrowser.Controllers;

/// <summary>
/// Relays a station stream through the Jellyfin server, so listeners on an HTTPS Jellyfin site can
/// play stations that only offer plain HTTP. Only URLs signed by this plugin are relayed.
/// </summary>
/// <remarks>
/// Anonymous because audio players cannot send Jellyfin authentication headers; the HMAC signature
/// takes the place of authentication.
/// </remarks>
[ApiController]
[Route("RadioBrowser/Relay")]
[AllowAnonymous]
public class RadioRelayController : ControllerBase
{
    /// <summary>
    /// Name of the HTTP client used to fetch station streams.
    /// </summary>
    public const string HttpClientName = "RadioBrowserRelay";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<RadioRelayController> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RadioRelayController"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="logger">The logger.</param>
    public RadioRelayController(IHttpClientFactory httpClientFactory, ILogger<RadioRelayController> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Streams a station through the server.
    /// </summary>
    /// <param name="u">The encoded stream URL.</param>
    /// <param name="s">The signature.</param>
    /// <response code="200">Audio stream.</response>
    /// <response code="403">The signature is invalid.</response>
    /// <response code="502">The station could not be reached.</response>
    /// <returns>The audio stream.</returns>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult> Relay([FromQuery, Required] string u, [FromQuery, Required] string s)
    {
        var plugin = Plugin.Instance;
        if (plugin is null || !RelaySigner.TryVerify(u, s, plugin.GetRelaySecret(), out var streamUri) || streamUri is null)
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        var cancellationToken = HttpContext.RequestAborted;
        using var request = new HttpRequestMessage(HttpMethod.Get, streamUri);
        var version = typeof(RadioRelayController).Assembly.GetName().Version?.ToString() ?? "1.0";
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("Jellyfin-RadioBrowser", version));

        // Ask like an audio player: pass on what the listener's player accepts, never JSON first.
        var accept = Request.Headers.Accept.ToString();
        if (string.IsNullOrWhiteSpace(accept) || !request.Headers.TryAddWithoutValidation("Accept", accept))
        {
            request.Headers.Accept.ParseAdd("*/*");
        }

        HttpResponseMessage? response = null;
        try
        {
            response = await _httpClientFactory.CreateClient(HttpClientName)
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException ex)
        {
            response?.Dispose();
            _logger.LogWarning("Relaying {Url} failed: {Message}", streamUri, ex.Message);
            return StatusCode(StatusCodes.Status502BadGateway);
        }

        // The upstream response lives as long as the listener stays connected.
        HttpContext.Response.RegisterForDispose(response);
        HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
        Response.Headers.CacheControl = "no-cache, no-store";

        var contentType = response.Content.Headers.ContentType?.ToString();
        _logger.LogInformation(
            "Relaying {Url}: HTTP {Status}, {ContentType}",
            streamUri,
            (int)response.StatusCode,
            contentType ?? "no content type");
        if (string.IsNullOrEmpty(contentType) || contentType.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
        {
            contentType = "audio/mpeg";
        }

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return new FileStreamResult(stream, contentType) { EnableRangeProcessing = false };
    }
}
