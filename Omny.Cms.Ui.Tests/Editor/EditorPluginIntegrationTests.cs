using Omny.Cms.Plugins.Fields;
using Omny.Cms.Editor.Plugins;
using Omny.Cms.UiRepositories.Files;
using Moq;
using Microsoft.Extensions.DependencyInjection;
using Omny.Cms.Editor;
using Omny.Cms.Editor.ContentTypes;
using Omny.Cms.Editor.Fields;
using Omny.Cms.Manifest;
using NUnit.Framework;
using Omny.Cms.UiImages.Services;

namespace Omny.Cms.Ui.Tests.Editor;

public class EditorPluginIntegrationTests
{
    [Test]
    public void EditorPluginRegistry_CanResolveAllPluginTypes()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<IFieldPlugin, MarkdownEditorPlugin>();
        services.AddScoped<IFieldPlugin,TinyMceHtmlEditorPlugin>();
        services.AddScoped<IFieldPlugin,QuillHtmlEditorPlugin>();
        services.AddScoped<IFieldPlugin,TextEditorPlugin>();
        services.AddScoped<IPluginRegistry, PluginRegistry>();
        services.AddScoped<IEditorService>(_ => new Mock<IEditorService>().Object);
        
        var serviceProvider = services.BuildServiceProvider();
        var registry = serviceProvider.GetRequiredService<IPluginRegistry>();

        // Act & Assert - Test that we can get plugins by type
        var markdownPlugin = registry.GetEditorPlugin(typeof(MarkdownEditorPlugin));
        var htmlPlugin = registry.GetEditorPlugin(typeof(TinyMceHtmlEditorPlugin));
        var textPlugin = registry.GetEditorPlugin(typeof(TextEditorPlugin));

        Assert.IsNotNull(markdownPlugin);
        Assert.IsNotNull(htmlPlugin);
        Assert.IsNotNull(textPlugin);
        
        Assert.IsInstanceOf<MarkdownEditorPlugin>(markdownPlugin);
        Assert.IsInstanceOf<TinyMceHtmlEditorPlugin>(htmlPlugin);
        Assert.IsInstanceOf<TextEditorPlugin>(textPlugin);
    }

    [TestCase("document.md", typeof(MarkdownEditorPlugin))]
    [TestCase("readme.markdown", typeof(MarkdownEditorPlugin))]
    [TestCase("page.html", typeof(TinyMceHtmlEditorPlugin))]
    [TestCase("index.htm", typeof(TinyMceHtmlEditorPlugin))]
    [TestCase("config.json", typeof(TextEditorPlugin))]
    [TestCase("styles.css", typeof(TextEditorPlugin))]
    [TestCase("script.js", typeof(TextEditorPlugin))]
    [TestCase("data.xml", typeof(TextEditorPlugin))]
    [TestCase("notes.txt", typeof(TextEditorPlugin))]
    [TestCase("unknown.xyz", typeof(TextEditorPlugin))]
    public void ManifestEditorService_SelectsCorrectPluginForFileType(string fileName, Type expectedPluginType)
    {
        // Arrange
        var manifest = new OmnyManifest { HtmlEditor = "tinymce" };
        var services = new ServiceCollection();
        services.AddMajorPlugins();
        var prov = services.BuildServiceProvider();
        var service = new ManifestEditorService(Mock.Of<IRemoteFileService>(), Mock.Of<IImageStorageService>(), prov.GetServices<IContentTypePlugin>(), prov.GetRequiredService<IContentTypeSerializer>(), new Core.Editor.ManifestProvider(), manifest);

        // Act
        var actualPluginType = service.GetEditorPluginType(fileName);

        // Assert
        Assert.AreEqual(expectedPluginType, actualPluginType);
    }
}