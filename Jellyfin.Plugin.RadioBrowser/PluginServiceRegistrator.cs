using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using Jellyfin.Plugin.RadioBrowser.Api;
using Jellyfin.Plugin.RadioBrowser.Channels;
using Jellyfin.Plugin.RadioBrowser.Controllers;
using Jellyfin.Plugin.RadioBrowser.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.RadioBrowser;

/// <summary>
/// Registers the plugin services with the Jellyfin dependency injection container.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<RadioBrowserClient>();

        // Channels are not discovered automatically: every IChannel must be registered.
        serviceCollection.AddSingleton<IChannel, RadioBrowserChannel>();

        serviceCollection.AddHostedService<ClickReporter>();
        serviceCollection.AddHostedService<SettingsWatcher>();

        // Own client for the stream relay. Jellyfin's shared client asks for JSON first and for
        // compressed responses, which some streaming servers answer with something other than audio.
        serviceCollection.AddHttpClient(RadioRelayController.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AutomaticDecompression = DecompressionMethods.None,
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 10,
                ConnectTimeout = TimeSpan.FromSeconds(10),
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                ActivityHeadersPropagator = DistributedContextPropagator.CreateNoOutputPropagator()
            });
    }
}
