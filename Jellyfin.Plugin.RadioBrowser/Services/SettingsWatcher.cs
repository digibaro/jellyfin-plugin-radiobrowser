using System;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Hosting;

namespace Jellyfin.Plugin.RadioBrowser.Services;

/// <summary>
/// Starts <see cref="RefreshStationsTask"/> whenever the plugin settings are saved, so changes
/// reach every stored station instead of only the folders someone opens.
/// </summary>
public sealed class SettingsWatcher : IHostedService
{
    private readonly ITaskManager _taskManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsWatcher"/> class.
    /// </summary>
    /// <param name="taskManager">The task manager.</param>
    public SettingsWatcher(ITaskManager taskManager)
    {
        _taskManager = taskManager;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (Plugin.Instance is not null)
        {
            Plugin.Instance.ConfigurationChanged += OnConfigurationChanged;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (Plugin.Instance is not null)
        {
            Plugin.Instance.ConfigurationChanged -= OnConfigurationChanged;
        }

        return Task.CompletedTask;
    }

    private void OnConfigurationChanged(object? sender, BasePluginConfiguration e)
    {
        _taskManager.QueueScheduledTask<RefreshStationsTask>();
    }
}
