using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using Omny.Cms.Editor;
using Omny.Cms.Editor.ContentTypes;
using Omny.Cms.Editor.Fields;
using Omny.Cms.Editor.Plugins;
using Omny.Cms.Plugins.Fields;
using Omny.Cms.Plugins.Page;
using Omny.Cms.Plugins.Menu;
using Omny.Cms.Plugins.Hexo;

namespace Omny.Cms.Ui.Tests.Editor;

public static class ServiceCollectionExtensions
{
    public static void AddMajorPlugins( this IServiceCollection services)
    {
        services.AddScoped<IContentTypePlugin, PagePlugin>();
        services.AddScoped<IContentTypePlugin, MenuPlugin>();
        services.AddScoped<IContentTypePlugin, HexoPostPlugin>();
        services.AddSingleton<IFieldPlugin, MarkdownEditorPlugin>();
        services.AddSingleton<IFieldPlugin, TinyMceHtmlEditorPlugin>();
        services.AddSingleton<IFieldPlugin, TextEditorPlugin>();
        services.AddSingleton<IFieldPlugin, Cms.Editor.Fields.ImageFieldPlugin>();
        services.AddSingleton<IFieldPlugin, Cms.Editor.Fields.ImageTextFieldPlugin>();
        services.AddScoped<IDialogService, DialogService>();
        services.AddSingleton<IPluginRegistry, PluginRegistry>();

        services.AddSingleton<IContentTypeSerializer, ContentTypeSerializer>();
        services.AddScoped<DefaultContentTypeSerializer>();
        services.AddSingleton<IContentTypeSerializerPlugin, DefaultContentTypeSerializer>();
        services.AddSingleton<IContentTypeSerializerPlugin, HexoPostSerializer>();
    }
}