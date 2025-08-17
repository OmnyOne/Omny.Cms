using Omny.Cms.Editor.ContentTypes;
using Omny.Cms.Manifest;
using Omny.Cms.Files;
using System.IO;

namespace Omny.Cms.Plugins.Page;

public class PagePlugin : IContentTypePlugin
{
    public string Name => "Page";
    public new string ContentType => "Omny.Page";

    public ContentTypeMetadata Metadata => new()
    {
        PluginType = "Omny.Page",
        Folder = "content/pages/",
        Fields =
        [
            new FieldDefinition("Path", "text", null, "URL Path", IsAdvanced: true),
            new FieldDefinition("Body", "HTML", ".html", "Body"),
            new FieldDefinition("MoreContent", "MoreContent", ".json", "More Content")
        ],
        FolderField = "Path"
    };

    public ContentTypeMetadata Configure(ContentTypePluginConfiguration config)
    {
        return Metadata with { Folder = config.Folder ?? Metadata.Folder };
    }
}
