using MudBlazor;
using Omny.Cms.Manifest;
using Omny.Cms.Editor.ContentTypes;
using System.Linq;

namespace Omny.Cms.Editor;

public static class ManifestLoader
{
    public static OmnyManifest LoadManifest(string? manifestJson, IEnumerable<IContentTypePlugin>? plugins = null)
    {
        OmnyManifest manifest = manifestJson is null
            ? new OmnyManifest
            {
                Name = "Default Manifest",
                Version = "2.0.0",
                FieldTypeDefinitions = new Dictionary<string, FieldTypeDefinition>
                {
                    ["ImageText"] = new FieldTypeDefinition(
                        "compound",
                        SubFields: [
                            new SubFieldDefinition("image", null),
                            new SubFieldDefinition("text", "Caption")
                        ]),
                    ["MoreContent"] = new FieldTypeDefinition("collection", ["ImageText", "html"])
                    ,
                    ["MenuItems"] = new FieldTypeDefinition("collection", ["MenuItem"]),
                    ["TextList"] = new FieldTypeDefinition("collection", ["text"])
                },
                ImageLocation = "content/images",
                RandomImageNames = false,
                MaxImageSizeKb = 1024,
                ContentTypeDefinitions = new Dictionary<string, ContentTypeMetadata>()
            }
            : System.Text.Json.JsonSerializer.Deserialize<OmnyManifest>(manifestJson)!;

        manifest.ImageLocation ??= "content/images";
        manifest.HtmlEditor ??= "tinymce";
        if (manifest.MaxImageSizeKb <= 0)
        {
            manifest.MaxImageSizeKb = 1024;
        }

        manifest.CustomData ??= new Dictionary<string, object>
        {
            {
                "PageTemplate",
                "content/template.html"
            },
            {
                "MenuItemType",
                "Page"
            },
            {
                "MenuItemNameField",
                "Name"
            },
            {
                "MenuItemLinkField",
                "Path"
            }
        };
        
        manifest.BuildConfiguration ??= new BuildConfiguration(
            OutputDirectory: "output",
            StaticAssetPaths: ["content/static"]
        );

        if (!manifest.ContentTypeDefinitions.Any()
            && plugins != null
            && manifest.ContentTypePluginConfigurations is null)
        {
            var pagePlugin = plugins.Single(p => p.ContentType == "Omny.Page");
            manifest.ContentTypeDefinitions["Page"] = pagePlugin.Configure(new ContentTypePluginConfiguration("Page", "content/pages/"));
            var menuPlugin = plugins.Single(p => p.ContentType == "Omny.Menu");
            manifest.ContentTypeDefinitions["Menu"] = menuPlugin.Configure(new ContentTypePluginConfiguration("Menu", "content/menus/"));
            var postPlugin = plugins.Single(p => p.ContentType == "Hexo.Post");
            manifest.ContentTypeDefinitions["Post"] = postPlugin.Configure(new ContentTypePluginConfiguration("Blog Post", "source/_posts/"));
        }

        foreach (var kv in manifest.ContentTypeDefinitions.ToList())
        {
            if (kv.Value.PluginType is null && plugins is not null)
            {
                var plug = plugins.FirstOrDefault(p => string.Equals(p.ContentType, kv.Key, StringComparison.OrdinalIgnoreCase) || string.Equals(p.Name, kv.Key, StringComparison.OrdinalIgnoreCase));
                if (plug != null)
                {
                    manifest.ContentTypeDefinitions[kv.Key] = kv.Value with { PluginType = plug.ContentType };
                }
            }
        }
        
        if (manifest.ContentTypePluginConfigurations is not null)
        {
            

         
            foreach (var plugin in plugins)
            {
                if (manifest.ContentTypePluginConfigurations.TryGetValue(plugin.ContentType, out var configs))
                {
                    foreach (var cfg in configs)
                    {
                        if (!manifest.ContentTypeDefinitions.ContainsKey(cfg.Name))
                        {
                            manifest.ContentTypeDefinitions[cfg.Name] = plugin.Configure(cfg);
                        }
                    }
                }
            }
            
        }

        return manifest;
    }
}