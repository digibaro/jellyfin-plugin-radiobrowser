using System;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.RadioBrowser.Api;
using Jellyfin.Plugin.RadioBrowser.Channels;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.RadioBrowser.Services;

/// <summary>
/// Reports station plays to radio-browser.info, as the API operator asks every client to do.
/// </summary>
/// <remarks>
/// Hooking playback start keeps the station's stream URL static, which avoids the duplicate
/// "versions" Jellyfin shows when a channel also supplies media sources at play time.
/// </remarks>
public sealed class ClickReporter : IHostedService, IDisposable
{
    private readonly ISessionManager _sessionManager;
    private readonly RadioBrowserClient _client;
    private readonly ILogger<ClickReporter> _logger;
    private readonly CancellationTokenSource _stopping = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ClickReporter"/> class.
    /// </summary>
    /// <param name="sessionManager">The session manager.</param>
    /// <param name="client">The Radio Browser client.</param>
    /// <param name="logger">The logger.</param>
    public ClickReporter(ISessionManager sessionManager, RadioBrowserClient client, ILogger<ClickReporter> logger)
    {
        _sessionManager = sessionManager;
        _client = client;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _sessionManager.PlaybackStart += OnPlaybackStart;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _sessionManager.PlaybackStart -= OnPlaybackStart;
        await _stopping.CancelAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _stopping.Dispose();
    }

    private void OnPlaybackStart(object? sender, PlaybackProgressEventArgs e)
    {
        var item = e.Item;
        if (item is null
            || item.SourceType != SourceType.Channel
            || !Plugin.CurrentConfiguration.ReportClicks
            || item.ProviderIds is null
            || !item.ProviderIds.TryGetValue(StationMapper.ProviderKey, out var stationUuid))
        {
            return;
        }

        _logger.LogDebug("Reporting play of {Station} to Radio Browser", item.Name);

        // Fire and forget: playback must never wait for the directory service.
        _ = _client.ReportClickAsync(stationUuid, _stopping.Token);
    }
}
