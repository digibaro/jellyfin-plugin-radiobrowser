using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.RadioBrowser.Channels;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.RadioBrowser.Services;

/// <summary>
/// Lists every Internet Radio folder Jellyfin has stored, so all stations get their current
/// stream link and details without anyone opening each folder.
/// </summary>
/// <remarks>
/// Jellyfin stores a copy of each station per folder and only rewrites it when that folder is
/// listed again. Runs automatically after the plugin settings are saved, and can be started from
/// Dashboard → Scheduled Tasks.
/// </remarks>
public class RefreshStationsTask : IScheduledTask
{
    private readonly IChannelManager _channelManager;
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<RefreshStationsTask> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RefreshStationsTask"/> class.
    /// </summary>
    /// <param name="channelManager">The channel manager.</param>
    /// <param name="libraryManager">The library manager.</param>
    /// <param name="logger">The logger.</param>
    public RefreshStationsTask(IChannelManager channelManager, ILibraryManager libraryManager, ILogger<RefreshStationsTask> logger)
    {
        _channelManager = channelManager;
        _libraryManager = libraryManager;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "Refresh Internet Radio stations";

    /// <inheritdoc />
    public string Key => "RadioBrowserRefreshStations";

    /// <inheritdoc />
    public string Description => "Updates the stream links and details of all stored Internet Radio stations.";

    /// <inheritdoc />
    public string Category => "Internet Radio";

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(progress);

        var plugin = Plugin.Instance;
        if (plugin is null)
        {
            return;
        }

        // A new stamp changes the channel cache key (forces fresh listings) and the stations'
        // modification date (forces Jellyfin to rewrite their stored stream information).
        plugin.Configuration.SettingsChangedUtc = DateTime.UtcNow;
        plugin.SaveConfiguration();

        var channelId = _libraryManager.GetNewItemId("Channel " + RadioBrowserChannel.ChannelName, typeof(Channel));

        // Root first, so renamed or new top-level folders exist before their contents are listed.
        await ListAsync(channelId, Guid.Empty, cancellationToken).ConfigureAwait(false);

        var folders = _libraryManager.GetItemList(new InternalItemsQuery
        {
            ChannelIds = [channelId],
            IsFolder = true
        }).Select(f => f.Id).ToList();

        _logger.LogInformation("Refreshing {Count} Internet Radio folders", folders.Count);

        for (var i = 0; i < folders.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // A folder may have been removed by an earlier listing in this run.
            if (_libraryManager.GetItemById(folders[i]) is not null)
            {
                await ListAsync(channelId, folders[i], cancellationToken).ConfigureAwait(false);
            }

            progress.Report(100.0 * (i + 1) / folders.Count);
        }
    }

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() => [];

    private async Task ListAsync(Guid channelId, Guid parentId, CancellationToken cancellationToken)
    {
        try
        {
            await _channelManager.GetChannelItemsInternal(
                new InternalItemsQuery
                {
                    ChannelIds = [channelId],
                    ParentId = parentId
                },
                new Progress<double>(),
                cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            // Keep going: one unreachable listing must not stop the others.
            _logger.LogWarning("Refreshing Internet Radio folder {FolderId} failed: {Message}", parentId, ex.Message);
        }
    }
}
