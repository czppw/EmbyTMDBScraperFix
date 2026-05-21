using System;
using System.IO;
using System.Reflection;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Plugins;

namespace EmbyTMDBScraperFix.Configuration;

/// <summary>
/// Dashboard configuration page. Emby discovers this type automatically and exposes it in the plugin UI.
/// </summary>
public sealed class EmbyTMDBScraperFixConfigurationPage : IPluginConfigurationPage
{
    private const string ResourceName = "EmbyTMDBScraperFix.Web.configuration.html";

    public string Name => EmbyTMDBScraperFix.Plugin.PluginDisplayName;

    public ConfigurationPageType ConfigurationPageType => ConfigurationPageType.PluginConfiguration;

    public IPlugin Plugin => EmbyTMDBScraperFix.Plugin.Instance ?? throw new InvalidOperationException("Plugin instance is not available.");

    public Stream GetHtmlStream()
    {
        var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName);
        if (stream == null)
        {
            throw new FileNotFoundException($"Embedded resource not found: {ResourceName}");
        }

        return stream;
    }
}
