using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Omny.Cms.Builder.Services;
using Omny.Cms.Editor;
using Omny.Cms.Editor.ContentTypes;
using Omny.Cms.Files;
using Omny.Cms.Plugins.Page;
using Omny.Cms.Plugins.Menu;
using Omny.Cms.Plugins.Hexo;
using Omny.Cms.Rendering.ContentRendering;
using Omny.Cms.Rendering;
using Microsoft.Extensions.DependencyInjection;

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
        services.AddSingleton<IContentTypeSerializer, ContentTypeSerializer>();
        services.AddSingleton<DefaultContentTypeSerializer>();
        services.AddSingleton<IContentTypeRenderer, OmnyPageRenderer>();
        services.TryAddSingleton<ITemplatePlugin, DefaultTemplatePlugin>();
        services.AddSingleton<IContentTypeSerializerPlugin, DefaultContentTypeSerializer>();
        services.AddSingleton<IContentTypeSerializerPlugin, HexoPostSerializer>();
        services.AddSingleton<ContentBuilder>();
        services.AddSingleton<IFileSystem>(sp => new LocalFileSystem(Directory.GetCurrentDirectory()));
        return services;
    }

    public static IServiceCollection AddPluginsFromAssembly(this IServiceCollection services, Assembly assembly)
    {
        foreach (var rendererType in assembly
            .GetTypes()
            .Where(t => typeof(IContentTypeRenderer).IsAssignableFrom(t) && t.IsClass && !t.IsAbstract))
        {
            services.AddSingleton(typeof(IContentTypeRenderer), rendererType);
        }

        foreach (var pluginType in assembly
            .GetTypes()
            .Where(t => typeof(IContentTypePlugin).IsAssignableFrom(t) && t.IsClass && !t.IsAbstract))
        {
            services.AddSingleton(typeof(IContentTypePlugin), pluginType);
        }

        foreach (var pluginType in assembly
            .GetTypes()
            .Where(t => typeof(IPagePlugin).IsAssignableFrom(t) && t.IsClass && !t.IsAbstract))
        {
            services.AddSingleton(typeof(IPagePlugin), pluginType);
        }

        foreach (var pluginType in assembly
            .GetTypes()
            .Where(t => typeof(ITemplatePlugin).IsAssignableFrom(t) && t.IsClass && !t.IsAbstract))
        {
            services.AddSingleton(typeof(ITemplatePlugin), pluginType);
        }

        return services;
    }

    public static IServiceCollection AddPluginsFromAssemblies(this IServiceCollection services, IEnumerable<Assembly> assemblies)
    {
        foreach (var assembly in assemblies)
        {
            services.AddPluginsFromAssembly(assembly);
        }

        return services;
    }
}
