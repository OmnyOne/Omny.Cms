using System.CommandLine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Omny.Cms.Builder.Services;
using Omny.Cms.Files;

namespace Omny.Cms.Builder;

public record OmnyBuilderDefaults(
    DirectoryInfo? Folder = null,
    bool Watch = false,
    bool Debug = false,
    FileInfo[]? Plugins = null);

public static class HostApplicationBuilderExtensions
{
    public static IHostApplicationBuilder ConfigureOmnyBuilder(this IHostApplicationBuilder builder)
    {
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Services.AddBuilderServices();
        return builder;
    }

    public static async Task<int> RunOmnyBuilder(
        this IHostApplicationBuilder builder,
        OmnyBuilderDefaults? defaults = null,
        string[]? args = null,
        CancellationToken cancellationToken = default)
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
            DefaultValueFactory = _ => defaults?.Plugins ?? Array.Empty<FileInfo>(),
        };

        var debugOption = new Option<bool>("--debug")
        {
            DefaultValueFactory = _ => defaults?.Debug ?? false,
        };

        var root = new RootCommand("Omny CMS Builder");
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
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                Console.CancelKeyPress += (s, e) =>
                {
                    e.Cancel = true;
                    cts.Cancel();
                };

                await svc.WatchAsync(path, cts.Token);
            }
        });

        string[] parsedArgs = args ?? Environment.GetCommandLineArgs().Skip(1).ToArray();
        ParseResult parseResult = root.Parse(parsedArgs);
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
