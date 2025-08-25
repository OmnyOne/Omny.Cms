using Omny.Cms.Editor;
using Omny.Cms.Plugins.Fields;
using Omny.Cms.Editor.Plugins;
using Moq;
using MudBlazor;
using Omny.Cms.Manifest;
using Omny.Cms.UiRepositories.Files;
using Omny.Cms.UiRepositories.Services;
using Microsoft.Extensions.DependencyInjection;
using Omny.Cms.Editor.ContentTypes;
using Omny.Cms.Plugins.Page;
using Omny.Cms.Plugins.Menu;
using Omny.Cms.Plugins.Hexo;
using NUnit.Framework;
using Omny.Cms.Editor.Fields;
using Omny.Cms.UiImages.Services;

namespace Omny.Cms.Ui.Tests.Editor;

public class EditorPluginTests
{
    [Test]
    public void MarkdownEditorPlugin_CanHandle_MarkdownExtensions()
    {
        // Arrange
        var plugin = new MarkdownEditorPlugin();

        // Act & Assert
        Assert.IsTrue(plugin.CanHandle(".md"));
        Assert.IsTrue(plugin.CanHandle(".markdown"));
        Assert.IsTrue(plugin.CanHandle(".MD"));
        Assert.IsFalse(plugin.CanHandle(".html"));
        Assert.IsFalse(plugin.CanHandle(".txt"));
    }

    [Test]
    public void HtmlEditorPlugin_CanHandle_HtmlExtensions()
    {
        // Arrange
        var plugin = new TinyMceHtmlEditorPlugin();

        // Act & Assert
        Assert.IsTrue(plugin.CanHandle(".html"));
        Assert.IsTrue(plugin.CanHandle(".htm"));
        Assert.IsTrue(plugin.CanHandle(".HTML"));
        Assert.IsFalse(plugin.CanHandle(".md"));
        Assert.IsFalse(plugin.CanHandle(".txt"));
    }

    [Test]
    public void TextEditorPlugin_CanHandle_TextExtensions()
    {
        // Arrange
        var plugin = new TextEditorPlugin();

        // Act & Assert
        Assert.IsTrue(plugin.CanHandle(".txt"));
        Assert.IsTrue(plugin.CanHandle(".json"));
        Assert.IsTrue(plugin.CanHandle(".xml"));
        Assert.IsTrue(plugin.CanHandle(".css"));
        Assert.IsTrue(plugin.CanHandle(".js"));
        Assert.IsFalse(plugin.CanHandle(".md"));
        Assert.IsFalse(plugin.CanHandle(".html"));
    }

    [Test]
    public void ManifestEditorService_GetEditorPluginType_ReturnsCorrectPluginTypes()
    {
        // Arrange
        var manifest = new OmnyManifest { HtmlEditor = "tinymce" };
        var sc = new ServiceCollection();
        sc.AddMajorPlugins();
        var prov = sc.BuildServiceProvider();

        var service = new ManifestEditorService(Mock.Of<IRemoteFileService>(), Mock.Of<IImageStorageService>(), prov.GetServices<IContentTypePlugin>(), prov.GetRequiredService<IContentTypeSerializer>(), new Core.Editor.ManifestProvider(), manifest); // Remote service not needed here

        // Act & Assert
        Assert.AreEqual(typeof(MarkdownEditorPlugin), service.GetEditorPluginType("test.md"));
        Assert.AreEqual(typeof(MarkdownEditorPlugin), service.GetEditorPluginType("test.markdown"));
        Assert.AreEqual(typeof(TinyMceHtmlEditorPlugin), service.GetEditorPluginType("test.html"));
        Assert.AreEqual(typeof(TinyMceHtmlEditorPlugin), service.GetEditorPluginType("test.htm"));
        Assert.AreEqual(typeof(TextEditorPlugin), service.GetEditorPluginType("test.txt"));
        Assert.AreEqual(typeof(TextEditorPlugin), service.GetEditorPluginType("test.json"));
        Assert.AreEqual(typeof(TextEditorPlugin), service.GetEditorPluginType("unknown.xyz"));
    }

    [Test]
    public void EditorPlugins_HaveUniqueNames()
    {
        // Arrange
        var markdownPlugin = new MarkdownEditorPlugin();
        var htmlPlugin = new TinyMceHtmlEditorPlugin();
        var textPlugin = new TextEditorPlugin();

        // Act & Assert
        Assert.AreEqual("Markdown", markdownPlugin.FieldType);
        Assert.AreEqual("HTML", htmlPlugin.FieldType);
        Assert.AreEqual("TextArea", textPlugin.FieldType);

        // Ensure all names are unique
        var names = new[] { markdownPlugin.FieldType, htmlPlugin.FieldType, textPlugin.FieldType };
        Assert.AreEqual(names.Length, names.Distinct().Count());
    }

    [Test]
    public void Plugins_Expose_DefaultValues()
    {
        var markdownPlugin = new MarkdownEditorPlugin();
        var htmlPlugin = new TinyMceHtmlEditorPlugin();
        var textPlugin = new TextEditorPlugin();
        var imagePlugin = new Cms.Editor.Fields.ImageFieldPlugin(Mock.Of<IDialogService>());
        var imageText = new Cms.Editor.Fields.ImageTextFieldPlugin();

        Assert.AreEqual(string.Empty, markdownPlugin.DefaultValue);
        Assert.AreEqual(string.Empty, htmlPlugin.DefaultValue);
        Assert.AreEqual(string.Empty, textPlugin.DefaultValue);
        Assert.AreEqual(string.Empty, imagePlugin.DefaultValue);
        Assert.IsNotNull(imageText.DefaultValue);
    }

    [Test]
    public void Plugins_Provide_FriendlyName_And_Icon()
    {
        var plugin = new Cms.Editor.Fields.ImageTextFieldPlugin();
        Assert.AreEqual("Image + Text", plugin.DisplayName);
        Assert.IsFalse(string.IsNullOrWhiteSpace(plugin.Icon));
    }
}