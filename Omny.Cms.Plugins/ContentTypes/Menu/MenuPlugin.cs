using Omny.Cms.Editor.ContentTypes;
using Omny.Cms.Manifest;
using MudBlazor;

namespace Omny.Cms.Plugins.Menu;

public class MenuPlugin : IContentTypePlugin
{
    public string Name => "Menu";
    public string ContentType => "Omny.Menu";

    public ContentTypeMetadata Metadata => new()
    {
        PluginType = "Omny.Menu",
        Folder = "content/menus/",
        Fields =
        [
            new FieldDefinition("Items", "MenuItems", ".json", "Items")
        ],
        Icon = MudBlazor.Icons.Material.Filled.Menu
    };

    public ContentTypeMetadata Configure(ContentTypePluginConfiguration config)
    {
        return Metadata with { Folder = config.Folder ?? Metadata.Folder };
    }
}
