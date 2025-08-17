using System.Text.Json;
using Moq;
using Omny.Cms.Editor;
using Omny.Cms.UiRepositories.Files;
using Omny.Cms.UiRepositories.Models;
using Omny.Cms.UiRepositories.Services;
using Microsoft.Extensions.DependencyInjection;
using Omny.Cms.Editor.ContentTypes;
using Omny.Cms.Plugins.Page;
using Omny.Cms.Plugins.Menu;
using Omny.Cms.Plugins.Hexo;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Omny.Cms.Editor.Fields;
using Omny.Cms.Manifest;
using Omny.Cms.UiImages.Services;

namespace Omny.Cms.Ui.Tests.Editor;

public class ContentTypeLogicTests
{
    private class InMemoryRemoteService
    {
        public Dictionary<string, string> Files { get; } = new();
        public Mock<IRemoteFileService> Mock { get; }

        public InMemoryRemoteService()
        {
            Mock = new Mock<IRemoteFileService>();
            Mock.Setup(s => s.GetFilesAsync()).ReturnsAsync(() =>
                Files.Keys.Select(k => new CacheableTreeItem(k, "hash")));
            Mock.Setup(s => s.GetFileContentsAsync(It.IsAny<string>())).ReturnsAsync((string path) =>
                new RemoteFileContents("main", path, Files.TryGetValue(path, out var c) ? c : null));
            Mock.Setup(s => s.GetLatestMarkdownContentAsync()).ReturnsAsync((string?)null);
            Mock.Setup(s => s.WriteFilesAsync(It.IsAny<Dictionary<string, string>>()))
                .Callback((Dictionary<string, string> dict) =>
                {
                    foreach (var kv in dict)
                    {
                        Files[kv.Key] = kv.Value;
                    }
                })
                .Returns(Task.CompletedTask);

            Mock.Setup(s => s.WriteBinaryFilesAsync(It.IsAny<Dictionary<string, byte[]>>()))
                .Callback((Dictionary<string, byte[]> dict) =>
                {
                    foreach (var kv in dict)
                    {
                        Files[kv.Key] = System.Convert.ToBase64String(kv.Value);
                    }
                })
                .Returns(Task.CompletedTask);

            Mock.Setup(s => s.DeleteFilesAsync(It.IsAny<string[]>()))
                .Callback((string[] paths) =>
                {
                    foreach (var p in paths)
                    {
                        Files.Remove(p);
                    }
                })
                .Returns(Task.CompletedTask);

            Mock.Setup(s => s.DeleteFolderAsync(It.IsAny<string>()))
                .Callback((string folder) =>
                {
                    var toRemove = Files.Keys.Where(k => k.StartsWith(folder)).ToList();
                    foreach (var p in toRemove)
                    {
                        Files.Remove(p);
                    }
                })
                .Returns(Task.CompletedTask);
        }
    }

    private static OmnyManifest CreateManifest(bool useFolderField = false)
    {
        return new OmnyManifest
        {
            Name = "Test",
            Version = "2.0.0",
            HtmlEditor = "tinymce",
            FieldTypeDefinitions = new Dictionary<string, FieldTypeDefinition>
            {
                ["ImageText"] = new FieldTypeDefinition(
                    "compound",
                    SubFields: [
                        new SubFieldDefinition("image", null),
                        new SubFieldDefinition("text", "Caption")
                    ]),
                ["MoreContent"] = new FieldTypeDefinition("collection", new[] { "ImageText", "html" })
            },
            ContentTypeDefinitions = new Dictionary<string, ContentTypeMetadata>
            {
                {
                    "Page",
                    new ContentTypeMetadata
                    {
                        PluginType = "Omny.Page",
                        Folder = "pages/",
                        FolderField = useFolderField ? "Path" : null,
                        Fields = new []
                        {
                            new FieldDefinition("Title","text",null,"Title"),
                            new FieldDefinition("Path","text",null,"Path"),
                            new FieldDefinition("Body","html",".html","Body"),
                            new FieldDefinition("MoreContent","MoreContent",".json","More Content")
                        }
                    }
                }
            }
        };
    }

    [Test]
    public async Task CanCreateSaveLoadAndEditContentItem()
    {
        var remote = new InMemoryRemoteService();
        var manifest = CreateManifest();
        var services = new ServiceCollection();
        services.AddScoped<IPluginRegistry, PluginRegistry>();
        services.AddScoped<IContentTypePlugin, PagePlugin>();
        services.AddScoped<IContentTypePlugin, MenuPlugin>();
        services.AddSingleton<IEditorService>(_ => new Mock<IEditorService>().Object);
        services.AddMajorPlugins();
        var provider = services.BuildServiceProvider();
        var service = new ManifestEditorService(
            remote.Mock.Object,
            Mock.Of<IImageStorageService>(),
            provider.GetServices<IContentTypePlugin>(),
            provider.GetRequiredService<IContentTypeSerializer>(),
            new Core.Editor.ManifestProvider(),
            manifest);

        var item = await service.CreateContentItemAsync("Page", "home");
        var contents = new Dictionary<string, object>
        {
            ["Title"] = "Home",
            ["Path"] = "index",
            ["Body"] = "<p>hi</p>"
        };
        await service.SaveContentItemAsync("Page", item, contents);

        Assert.Contains("pages/home/Body.html", remote.Files.Keys);
        Assert.Contains("pages/home/fields.json", remote.Files.Keys);

        var items = (await service.GetContentItemsAsync("Page")).ToList();
        Assert.That(items, Has.Exactly(1).Items);
        var loaded = items[0];
        Assert.AreEqual("Home", (loaded.FieldValues?["Title"] as JsonElement?)?.GetString());
        Assert.AreEqual("index", (loaded.FieldValues?["Path"] as JsonElement?)?.GetString());

        contents["Title"] = "New Title";
        await service.SaveContentItemAsync("Page", loaded, contents);
        var items2 = (await service.GetContentItemsAsync("Page")).ToList();
        Assert.AreEqual("New Title", (items2[0].FieldValues?["Title"] as JsonElement?)?.GetString());
    }


    [Test]
    public async Task UsesFieldValueAsSubdirectory_WhenFolderFieldSpecified()
    {
        var remote = new InMemoryRemoteService();
        var manifest = CreateManifest(useFolderField: true);
        var services = new ServiceCollection();
        services.AddScoped<IPluginRegistry, PluginRegistry>();
        services.AddMajorPlugins();
        services.AddScoped<IContentTypePlugin, PagePlugin>();
        services.AddScoped<IContentTypePlugin, MenuPlugin>();
        var provider = services.BuildServiceProvider();
        
        var service = new ManifestEditorService(remote.Mock.Object, Mock.Of<IImageStorageService>(), provider.GetServices<IContentTypePlugin>(), provider.GetRequiredService<IContentTypeSerializer>(), new Core.Editor.ManifestProvider(), manifest);

        var item = await service.CreateContentItemAsync("Page", "home");
        var contents = new Dictionary<string, object>
        {
            ["Title"] = "Home",
            ["Path"] = "index",
            ["Body"] = "<p>hi</p>"
        };

        await service.SaveContentItemAsync("Page", item, contents);

        Assert.Contains("pages/index/Body.html", remote.Files.Keys);
        Assert.Contains("pages/index/fields.json", remote.Files.Keys);
    }

    [Test]
    public async Task SaveContentItem_DeletesOldFolder_WhenFolderChanges()
    {
        var remote = new InMemoryRemoteService();
        var manifest = CreateManifest(useFolderField: true);
        var services = new ServiceCollection();
        services.AddMajorPlugins();
        var provider = services.BuildServiceProvider();

        var service = new ManifestEditorService(remote.Mock.Object, Mock.Of<IImageStorageService>(), provider.GetServices<IContentTypePlugin>(), provider.GetRequiredService<IContentTypeSerializer>(), new Core.Editor.ManifestProvider(), manifest);

        var item = await service.CreateContentItemAsync("Page", "home");
        var contents = new Dictionary<string, object>
        {
            ["Title"] = "Home",
            ["Path"] = "home",
            ["Body"] = "<p>hi</p>"
        };

        await service.SaveContentItemAsync("Page", item, contents);

        var loaded = (await service.GetContentItemsAsync("Page")).First();
        contents["Path"] = "newhome";
        await service.SaveContentItemAsync("Page", loaded, contents);

        Assert.IsFalse(remote.Files.Keys.Any(k => k.StartsWith("pages/home/")));
        Assert.IsTrue(remote.Files.Keys.Any(k => k.StartsWith("pages/newhome/")));
    }

    [Test]
    public async Task DefaultManifest_IncludesMoreContentCollection()
    {
        var remote = new InMemoryRemoteService();
        var services = new ServiceCollection();
        services.AddMajorPlugins();
        var provider = services.BuildServiceProvider();
        var service = new ManifestEditorService(remote.Mock.Object, Mock.Of<IImageStorageService>(), provider.GetServices<IContentTypePlugin>(), provider.GetRequiredService<IContentTypeSerializer>(), new Core.Editor.ManifestProvider());

        var fields = await service.GetFieldDefinitionsAsync("Page");
        Assert.That(fields, Has.Some.Matches<FieldDefinition>(f => f.Name == "MoreContent" && f.FieldType == "MoreContent"));
    }

    [Test]
    public async Task DefaultManifest_FirstFieldIsPath()
    {
        var remote = new InMemoryRemoteService();
        var sc = new ServiceCollection();
        sc.AddMajorPlugins();
        var prov = sc.BuildServiceProvider();
        var service = new ManifestEditorService(remote.Mock.Object, Mock.Of<IImageStorageService>(), prov.GetServices<IContentTypePlugin>(), prov.GetRequiredService<IContentTypeSerializer>(), new Core.Editor.ManifestProvider());

        var fields = (await service.GetFieldDefinitionsAsync("Page")).ToList();
        Assert.IsTrue(fields.First().Name == "Path" && fields.First().FieldType == "text");
    }

    [Test]
    public async Task CanPersist_MoreContentCollection()
    {
        var remote = new InMemoryRemoteService();
        var manifest = CreateManifest();
        var sc3 = new ServiceCollection();
        sc3.AddMajorPlugins();
        sc3.AddScoped<IPluginRegistry, PluginRegistry>();
        sc3.AddScoped<PagePlugin>();
        sc3.AddScoped<MenuPlugin>();
        sc3.AddScoped<IContentTypePlugin>(sp => sp.GetRequiredService<PagePlugin>());
        sc3.AddScoped<IContentTypePlugin>(sp => sp.GetRequiredService<MenuPlugin>());
        var provider = sc3.BuildServiceProvider();
        var service = new ManifestEditorService(remote.Mock.Object, Mock.Of<IImageStorageService>(), provider.GetServices<IContentTypePlugin>(), provider.GetRequiredService<IContentTypeSerializer>(), new Core.Editor.ManifestProvider(), manifest);

        var item = await service.CreateContentItemAsync("Page", "mc");
        var collection = new CollectionFieldContent(new List<FieldContent>
        {
            new("ImageText", JsonSerializer.Serialize(new { image = "img1.png", text = "one" })),
            new("ImageText", JsonSerializer.Serialize(new { image = "img2.png", text = "two" })),
            new("html", "<p>hi</p>"),
            new("ImageText", JsonSerializer.Serialize(new { image = "img3.png", text = "three" }))
        });
        var contents = new Dictionary<string, object>
        {
            ["Title"] = "MC",
            ["Path"] = "mc",
            ["Body"] = "<p>body</p>",
            ["MoreContent"] = collection
        };

        await service.SaveContentItemAsync("Page", item, contents);

        Assert.Contains("pages/mc/fields.json", remote.Files.Keys);
        Assert.Contains("pages/mc/MoreContent-3.html", remote.Files.Keys);

        var fields = JsonSerializer.Deserialize<Dictionary<string, object>>(remote.Files["pages/mc/fields.json"]);
        Assert.IsNotNull(fields);
        var elem = (fields!["FieldValues"] as JsonElement?).Value;
        var arr = elem.GetProperty("MoreContent").EnumerateArray().ToArray();
        Assert.AreEqual(4, arr.Length);
        Assert.AreEqual("ImageText", arr[0].GetProperty("type").GetString());
        Assert.IsTrue(arr[0].TryGetProperty("value", out _));
        Assert.AreEqual("html", arr[2].GetProperty("type").GetString());
        Assert.AreEqual("MoreContent-3.html", arr[2].GetProperty("file").GetString());
    }

    [Test]
    public async Task CollectionField_CanRoundTripWithTypes()
    {
        var remote = new InMemoryRemoteService();
        var manifest = CreateManifest();
        var services = new ServiceCollection();
        services.AddMajorPlugins();
        var provider = services.BuildServiceProvider();
        var service = new ManifestEditorService(remote.Mock.Object, Mock.Of<IImageStorageService>(), provider.GetServices<IContentTypePlugin>(), provider.GetRequiredService<IContentTypeSerializer>(), new Core.Editor.ManifestProvider(), manifest);

        var item = await service.CreateContentItemAsync("Page", "round");
        var collection = new CollectionFieldContent(new List<FieldContent>
        {
            new("html", "<p>hi</p>"),
            new("ImageText", JsonSerializer.Serialize(new { image = "img.png", text = "t" }))
        });
        var contents = new Dictionary<string, object>
        {
            ["Title"] = "R",
            ["Path"] = "round",
            ["Body"] = "<p>body</p>",
            ["MoreContent"] = collection
        };

        await service.SaveContentItemAsync("Page", item, contents);

        var fieldsJson = JsonSerializer.Deserialize<Dictionary<string, object>>(remote.Files["pages/round/fields.json"])!;
        var elem = (fieldsJson["FieldValues"] as JsonElement?).Value;
        var arr = elem.GetProperty("MoreContent").EnumerateArray().ToArray();
        var dir = "pages/round";
        List<FieldContent> loaded = new();
        foreach (var entry in arr)
        {
            var type = entry.GetProperty("type").GetString()!;
            if (entry.TryGetProperty("file", out var f))
            {
                var content = remote.Files[System.IO.Path.Combine(dir, f.GetString()!).Replace("\\", "/")];
                loaded.Add(new FieldContent(type, content));
            }
            else if (entry.TryGetProperty("value", out var val))
            {
                loaded.Add(new FieldContent(type, val.GetRawText()));
            }
        }

        Assert.AreEqual(collection.Items.Select(i => i.FieldType), loaded.Select(i => i.FieldType));
        Assert.AreEqual(collection.Items.Select(i => i.Content), loaded.Select(i => i.Content));
    }

    [Test]
    public async Task PluginConfiguration_AddsAdditionalContentTypes()
    {
        var remote = new InMemoryRemoteService();
        var manifest = CreateManifest();
        manifest.ContentTypePluginConfigurations = new Dictionary<string, ContentTypePluginConfiguration[]>
        {
            ["Omny.Page"] = [new ContentTypePluginConfiguration("Secondary Page", "secondary/")]
        };

        var services = new ServiceCollection();
        services.AddMajorPlugins();
        var provider = services.BuildServiceProvider();
        var loaded = ManifestLoader.LoadManifest(System.Text.Json.JsonSerializer.Serialize(manifest), provider.GetServices<IContentTypePlugin>());
        var service = new ManifestEditorService(remote.Mock.Object, Mock.Of<IImageStorageService>(), provider.GetServices<IContentTypePlugin>(), provider.GetRequiredService<IContentTypeSerializer>(), new Core.Editor.ManifestProvider(), loaded);

        var types = (await service.GetContentTypesAsync()).Select(t => t.Name).ToList();

        Assert.Contains("Page", types);
        Assert.Contains("Secondary Page", types);
        Assert.Pass();
    }

    [Test]
    public async Task PluginConfiguration_ReplacesDefaultContentType()
    {
        var remote = new InMemoryRemoteService();
        var manifest = CreateManifest();
        manifest.ContentTypeDefinitions.Clear();
        manifest.ContentTypePluginConfigurations = new Dictionary<string, ContentTypePluginConfiguration[]>
        {
            ["Omny.Page"] = [new ContentTypePluginConfiguration("Alternate Page", "alt/")]
        };

        var services = new ServiceCollection();
        services.AddMajorPlugins();
        var provider = services.BuildServiceProvider();
        var loaded = ManifestLoader.LoadManifest(System.Text.Json.JsonSerializer.Serialize(manifest), provider.GetServices<IContentTypePlugin>());
        var service = new ManifestEditorService(remote.Mock.Object, Mock.Of<IImageStorageService>(), provider.GetServices<IContentTypePlugin>(), provider.GetRequiredService<IContentTypeSerializer>(), new Core.Editor.ManifestProvider(), loaded);

        var types = (await service.GetContentTypesAsync()).Select(t => t.Name).ToList();

        Assert.That(types, Does.Not.Contain("Page"));
        Assert.Contains("Alternate Page", types);
        Assert.Pass();
    }

}
