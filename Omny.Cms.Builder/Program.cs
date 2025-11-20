using System.CommandLine;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Omny.Cms.Builder;
using Omny.Cms.Builder.Services;
using Omny.Cms.Editor;
using Omny.Cms.Editor.ContentTypes;
using Omny.Cms.Files;
using Omny.Cms.Plugins.Hexo;
using Omny.Cms.Plugins.Infrastructure;
using Omny.Cms.Plugins.Menu;
using Omny.Cms.Plugins.Page;
using Omny.Cms.Rendering.ContentRendering;

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Services.AddBuilderServices();

var folderOption = new Option<DirectoryInfo?>(
    name: "--folder")
{
    DefaultValueFactory = (_) => new DirectoryInfo("."),
};
var watchOption = new Option<bool>("--watch")
{
    DefaultValueFactory = (_) => false
};

var pluginOption = new Option<FileInfo[]>("--plugin")
{
    AllowMultipleArgumentsPerToken = true,
    DefaultValueFactory = _ => Array.Empty<FileInfo>()
};

var root = new RootCommand("Omny CMS Builder");
var debugOption = new Option<bool>("--debug")
{
    DefaultValueFactory = _ => false
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
        Console.CancelKeyPress += (s, e) => { e.Cancel = true; cts.Cancel(); };
        await svc.WatchAsync(path, cts.Token);
    }
});

ParseResult parseResult = root.Parse(args);
return await parseResult.InvokeAsync();

static IEnumerable<Assembly> LoadPluginAssemblies(IEnumerable<FileInfo>? pluginFiles)
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
