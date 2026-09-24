using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net.Http;
using System.Net.Mime;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.RadioBrowser.Api;
using MediaBrowser.Common.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.RadioBrowser.Controllers;

/// <summary>
/// Administrator endpoints used by the plugin configuration page to pick featured stations.
/// </summary>
[ApiController]
[Route("RadioBrowser")]
[Authorize(Policy = Policies.RequiresElevation)]
[Produces(MediaTypeNames.Application.Json)]
public class RadioBrowserController : ControllerBase
{
    private readonly RadioBrowserClient _client;

    /// <summary>
    /// Initializes a new instance of the <see cref="RadioBrowserController"/> class.
    /// </summary>
    /// <param name="client">The Radio Browser client.</param>
    public RadioBrowserController(RadioBrowserClient client)
    {
        _client = client;
    }

    /// <summary>
    /// Searches stations by name.
    /// </summary>
    /// <param name="name">Part of the station name.</param>
    /// <param name="countryCode">Optional ISO 3166-1 alpha-2 country code.</param>
    /// <param name="limit">Maximum number of results.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <response code="200">Stations found.</response>
    /// <response code="502">The Radio Browser directory could not be reached.</response>
    /// <returns>The matching stations.</returns>
    [HttpGet("Search")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<IEnumerable<StationSummary>>> Search(
        [FromQuery, Required, MinLength(2)] string name,
        [FromQuery] string? countryCode,
        [FromQuery, Range(1, 100)] int limit = 30,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var stations = await _client.SearchStationsAsync(
                new StationQuery { Name = name, CountryCode = countryCode, Limit = limit },
                hideBroken: true,
                cancellationToken).ConfigureAwait(false);
            return Ok(stations.Select(StationSummary.From));
        }
        catch (HttpRequestException)
        {
            return StatusCode(StatusCodes.Status502BadGateway);
        }
    }

    /// <summary>
    /// Gets stations by UUID, in the given order.
    /// </summary>
    /// <param name="uuids">Comma separated station UUIDs.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <response code="200">Stations found.</response>
    /// <response code="502">The Radio Browser directory could not be reached.</response>
    /// <returns>The stations that still exist.</returns>
    [HttpGet("Stations")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<IEnumerable<StationSummary>>> GetStations(
        [FromQuery, Required] string uuids,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var stations = await _client.GetStationsByUuidAsync(uuids.Split(','), cancellationToken).ConfigureAwait(false);
            return Ok(stations.Select(StationSummary.From));
        }
        catch (HttpRequestException)
        {
            return StatusCode(StatusCodes.Status502BadGateway);
        }
    }
}
