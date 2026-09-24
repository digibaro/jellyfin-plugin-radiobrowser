using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Threading;
using Jellyfin.Plugin.RadioBrowser.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.RadioBrowser;

/// <summary>
/// The Internet Radio plugin entry point.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// The plugin identifier, identical to the guid in build.yaml.
    /// </summary>
    public const string PluginGuid = "077e1652-2e0c-461a-a6b4-cd3cb6e6c628";

    private readonly Lock _secretLock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Instance of the <see cref="IApplicationPaths"/> interface.</param>
    /// <param name="xmlSerializer">Instance of the <see cref="IXmlSerializer"/> interface.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <inheritdoc />
    public override string Name => "Internet Radio";

    /// <inheritdoc />
    public override string Description => "Browse and play internet radio stations from radio-browser.info.";

    /// <inheritdoc />
    public override Guid Id => Guid.Parse(PluginGuid);

    /// <summary>
    /// Gets the current plugin instance.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <summary>
    /// Gets the active configuration, or defaults if the plugin has not been loaded yet.
    /// </summary>
    public static PluginConfiguration CurrentConfiguration => Instance?.Configuration ?? new PluginConfiguration();

    /// <summary>
    /// Gets the secret used to sign relay URLs, creating and saving one on first use.
    /// </summary>
    /// <returns>The secret.</returns>
    public string GetRelaySecret()
    {
        var config = Configuration;
        if (string.IsNullOrEmpty(config.RelaySecret))
        {
            lock (_secretLock)
            {
                if (string.IsNullOrEmpty(config.RelaySecret))
                {
                    config.RelaySecret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
                    SaveConfiguration();
                }
            }
        }

        return config.RelaySecret;
    }

    /// <inheritdoc />
    public override void UpdateConfiguration(BasePluginConfiguration configuration)
    {
        if (configuration is PluginConfiguration updated)
        {
            // Stamp the save time so stored stream information of every station is refreshed.
            updated.SettingsChangedUtc = DateTime.UtcNow;
            if (string.IsNullOrEmpty(updated.RelaySecret))
            {
                updated.RelaySecret = Configuration.RelaySecret;
            }
        }

        base.UpdateConfiguration(configuration);
    }

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return
        [
            new PluginPageInfo
            {
                Name = Name,
                EmbeddedResourcePath = string.Format(CultureInfo.InvariantCulture, "{0}.Configuration.configPage.html", GetType().Namespace)
            }
        ];
    }
}
