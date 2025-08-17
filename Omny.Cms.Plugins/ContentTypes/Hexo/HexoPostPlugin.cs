using Omny.Cms.Editor.ContentTypes;
using Omny.Cms.Manifest;
using MudBlazor;

namespace Omny.Cms.Plugins.Hexo;

public class HexoPostPlugin : IContentTypePlugin
{
    public string Name { get; init; } = "Blog Post";
    public string ContentType => "Hexo.Post";

    public ContentTypeMetadata Metadata => new()
    {
        PluginType = "Hexo.Post",
        FileExtensionToIdMapping = new Dictionary<string, string>
        {
            [".md"] = "source/_posts/"
        },
        Fields =
        [
            new FieldDefinition("title", "text", null, "Title"),
            new FieldDefinition("date", "date", null, "Date"),
            new FieldDefinition("updated", "date", null, "Updated"),
            new FieldDefinition("tags", "TextList", null, "Tags"),
            new FieldDefinition("categories", "TextList", null, "Categories"),
            new FieldDefinition("Body", "Markdown", ".md", "Body")
        ],
        Icon = Icons.Material.Filled.Article
    };

    public ContentTypeMetadata Configure(ContentTypePluginConfiguration config)
    {
       return Metadata with
        {

            Folder = config.Folder ?? Metadata.Folder
        };
    }
}
