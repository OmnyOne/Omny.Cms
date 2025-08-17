using Omny.Cms.Builder.Services;
using Omny.Cms.Editor;
using Omny.Cms.Editor.ContentTypes;
using Omny.Cms.Files;
using Omny.Cms.Plugins.Page;
using Omny.Cms.Plugins.Menu;
using Omny.Cms.Plugins.Hexo;
using Omny.Cms.Rendering.ContentRendering;

namespace Omny.Cms.Builder;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddBuilderServices(this IServiceCollection services)
    {
        services.AddSingleton<PagePlugin>();
        services.AddSingleton<MenuPlugin>();
        services.AddSingleton<HexoPostPlugin>();
        services.AddSingleton<IContentTypePlugin,PagePlugin>();
        services.AddSingleton<IContentTypePlugin, MenuPlugin>();
        services.AddSingleton<IContentTypePlugin, HexoPostPlugin>();
        services.AddScoped<IContentTypeSerializer, ContentTypeSerializer>();
        services.AddScoped<DefaultContentTypeSerializer>();
        services.AddScoped<IContentTypeRenderer, OmnyPageRenderer>();
        services.AddScoped<IContentTypeSerializerPlugin, DefaultContentTypeSerializer>();
        services.AddScoped<IContentTypeSerializerPlugin, HexoPostSerializer>();
        services.AddSingleton<ContentBuilder>();
        services.AddSingleton<IFileSystem>(sp => new LocalFileSystem(Directory.GetCurrentDirectory()));
        return services;
    }
}