using System;
using System.Collections.Generic;
using System.IO;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Drawing;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using EmbyTMDBScraperFix.Configuration;

namespace EmbyTMDBScraperFix;

public sealed class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages, IHasThumbImage
{
    public static readonly Guid PluginId = new("b6b08b4b-1b7d-4f1a-9d6c-f1f6b1ef0a11");
    public const string PluginDisplayName = "EmbyTMDBScraperFix";
    private const string ThumbResourcePath = "EmbyTMDBScraperFix.Assets.thumb.png";

    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    public static Plugin? Instance { get; private set; }

    public override Guid Id => PluginId;

    public override string Name => PluginDisplayName;

    public override string Description => "HTTP proxy-aware TMDB/TVDB scraper provider with incremental auto-scan for Emby.";

    public ImageFormat ThumbImageFormat => ImageFormat.Png;

    public override void UpdateConfiguration(BasePluginConfiguration configuration)
    {
        base.UpdateConfiguration(configuration);
        try { PluginRuntime.Instance.ApplyConfiguration(); } catch { }
    }

    public IEnumerable<PluginPageInfo> GetPages()
    {
        yield return new PluginPageInfo
        {
            Name = "EmbyTMDBScraperFixConfiguration",
            EmbeddedResourcePath = "EmbyTMDBScraperFix.Web.configuration.html"
        };

        yield return new PluginPageInfo
        {
            Name = "EmbyTMDBScraperFixConfigurationjs",
            EmbeddedResourcePath = "EmbyTMDBScraperFix.Web.embytmdbscraperfix.js"
        };
    }

    public Stream GetThumbImage()
    {
        return GetType().Assembly.GetManifestResourceStream(ThumbResourcePath)
            ?? throw new InvalidOperationException($"Embedded plugin thumbnail '{ThumbResourcePath}' was not found.");
    }
}
