using System.CommandLine;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Omny.Cms.Builder.Services;
using Omny.Cms.Editor;
using Omny.Cms.Editor.ContentTypes;
using Omny.Cms.Files;
using Omny.Cms.Plugins.Hexo;
using Omny.Cms.Plugins.Infrastructure;
using Omny.Cms.Plugins.Menu;
using Omny.Cms.Plugins.Page;
using Omny.Cms.Rendering.ContentRendering;

namespace Omny.Cms.Builder;

public class OmnyBuilderDefaults
{
    public DirectoryInfo? Folder { get; init; }

    public bool? Watch { get; init; }

    public bool? Debug { get; init; }

    public IEnumerable<FileInfo>? PluginFiles { get; init; }
}

public static class HostApplicationBuilderExtensions
{
    public static HostApplicationBuilder ConfigureOmnyBuilder(this HostApplicationBuilder builder)
    {
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Services.AddBuilderServices();
        return builder;
    }

    public static async Task<int> RunOmnyBuilder(
        this HostApplicationBuilder builder,
        string[]? args = null,
        OmnyBuilderDefaults? defaults = null)
    {
        var folderOption = new Option<DirectoryInfo?>("--folder")
        {
            DefaultValueFactory = _ => defaults?.Folder ?? new DirectoryInfo("."),
        };
        var watchOption = new Option<bool>("--watch")
        {
            DefaultValueFactory = _ => defaults?.Watch ?? false,
        };

        var pluginOption = new Option<FileInfo[]>("--plugin")
        {
            AllowMultipleArgumentsPerToken = true,
            DefaultValueFactory = _ => defaults?.PluginFiles?.ToArray() ?? Array.Empty<FileInfo>(),
        };

        var root = new RootCommand("Omny CMS Builder");
        var debugOption = new Option<bool>("--debug")
        {
            DefaultValueFactory = _ => defaults?.Debug ?? false,
        };
        root.Options.Add(folderOption);
        root.Options.Add(watchOption);
        root.Options.Add(debugOption);
        root.Options.Add(pluginOption);
        root.SetAction(async parseResult =>
        {
            var folder = parseResult.GetValue(folderOption);
            var watch = parseResult.GetValue(watchOption);
            string path = folder?.FullName ?? Directory.GetCurrentDirectory();
            var debug = parseResult.GetValue(debugOption);
            if (debug)
            {
                builder.Logging.SetMinimumLevel(LogLevel.Debug);
            }
            else
            {
                builder.Logging.SetMinimumLevel(LogLevel.Information);
            }

            var pluginFiles = parseResult.GetValue(pluginOption);
            var pluginAssemblies = LoadPluginAssemblies(pluginFiles);
            builder.Services.AddPluginsFromAssemblies(pluginAssemblies);

            using var host = builder.Build();
            var fs = host.Services.GetRequiredService<IFileSystem>() as LocalFileSystem;
            if (fs != null)
            {
                fs.BasePath = path;
            }

            var svc = host.Services.GetRequiredService<ContentBuilder>();
            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            await svc.BuildAsync(path);
            if (watch)
            {
                logger.LogInformation("Watching for changes. Press Ctrl+C to exit.");
                using var cts = new CancellationTokenSource();
                Console.CancelKeyPress += (s, e) =>
                {
                    e.Cancel = true;
                    cts.Cancel();
                };
                await svc.WatchAsync(path, cts.Token);
            }
        });

        var arguments = args ?? Environment.GetCommandLineArgs().Skip(1).ToArray();
        ParseResult parseResult = root.Parse(arguments);
        return await parseResult.InvokeAsync();
    }

    private static IEnumerable<Assembly> LoadPluginAssemblies(IEnumerable<FileInfo>? pluginFiles)
    {
        if (pluginFiles == null)
        {
            yield break;
        }

        foreach (var plugin in pluginFiles)
        {
            if (plugin == null)
            {
                continue;
            }

            string fullPath = plugin.FullName;
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"Plugin not found at path {fullPath}");
            }

            yield return AssemblyLoadContext.Default.LoadFromAssemblyPath(fullPath);
        }
    }
}
