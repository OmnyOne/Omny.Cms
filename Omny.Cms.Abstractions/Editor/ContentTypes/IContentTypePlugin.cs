using Omny.Cms.Manifest;

namespace Omny.Cms.Editor.ContentTypes;

public interface IContentTypePlugin
{
    /// <summary>
    /// Unique plugin name used for enabling/disabling this plugin.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Content type name exposed by this plugin.
    /// </summary>
    string ContentType { get; }

    /// <summary>
    /// Metadata definition that will be added to the manifest when the plugin is enabled.
    /// </summary>
    ContentTypeMetadata Metadata { get; }

    /// <summary>
    /// Returns metadata customized by the provided configuration.
    /// </summary>
    ContentTypeMetadata Configure(ContentTypePluginConfiguration config);
}
