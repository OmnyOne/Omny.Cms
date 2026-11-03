using System;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.FileProviders;
using Omny.Cms.Editor;
using Omny.Cms.Editor.ContentTypes;
using Omny.Cms.Manifest;
using Microsoft.Extensions.Logging;

namespace Omny.Cms.Builder.Services;

public class ContentBuilder
{
    private readonly IEnumerable<IContentTypeRenderer> _renderers;
    private readonly IEnumerable<IContentTypePlugin> _plugins;
    private readonly IContentTypeSerializer _serializer;
    private readonly ILogger<ContentBuilder> _logger;

    public ContentBuilder(
        IEnumerable<IContentTypeRenderer> renderers,
        IEnumerable<IContentTypePlugin> plugins,
        IContentTypeSerializer serializer,
        ILogger<ContentBuilder> logger)
    {
        _renderers = renderers;
        _plugins = plugins;
        _serializer = serializer;
        _logger = logger;
    }

    public async Task BuildAsync(string folder)
    {
        _logger.LogInformation("Starting build in {Folder}", folder);
        var contentService = new FileSystemContentService(folder, _serializer, _plugins);
        OmnyManifest manifest = await contentService.GetManifestAsync();
        string outputDir = Path.Combine(folder, manifest.BuildConfiguration?.OutputDirectory ?? "dist");
        _logger.LogInformation("Output directory: {OutputDir}", outputDir);
        Directory.CreateDirectory(outputDir);

        foreach (var renderer in _renderers)
        {
            var match = manifest.ContentTypeDefinitions.FirstOrDefault(kv =>
                string.Equals(kv.Value.PluginType ?? kv.Key, renderer.ContentType, StringComparison.OrdinalIgnoreCase));
            if (string.IsNullOrEmpty(match.Key))
            {
                continue;
            }

            _logger.LogInformation("Rendering {ContentType} content...", match.Key);
            var items = await contentService.GetContentItemsAsync(match.Key);
            var itemList = items.ToList();

            foreach (var item in itemList)
            {
                string fileName = renderer.GetOutputFileName(item, manifest);
                string dest = Path.Combine(outputDir, fileName);
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                _logger.LogDebug("Writing {Destination}", dest);
                string content = renderer.RenderContentType(item, manifest);
                await File.WriteAllTextAsync(dest, content);
            }
        }

        if (manifest.BuildConfiguration?.StaticAssetPaths is { Length: > 0 } paths)
        {
            foreach (var rel in paths)
            {
                string src = Path.Combine(folder, rel);
                string dst = outputDir;
                if (Directory.Exists(src))
                {
                    _logger.LogInformation("Copying directory {Source} to {Destination}", src, dst);
                    CopyDirectory(src, dst, _logger);
                }
            }
        }

        // copy images to the root of the output directory
        string imageFolder = Path.Combine(folder, manifest.ImageLocation ?? "content/images");
        if (Directory.Exists(imageFolder))
        {
            _logger.LogInformation("Copying images from {ImageFolder}", imageFolder);
            foreach (var img in Directory.GetFiles(imageFolder))
            {
                string fileName = Path.GetFileName(img);
                string dest = Path.Combine(outputDir, fileName);
                File.Copy(img, dest, true);
            }
        }

        _logger.LogInformation("Build complete.");
    }

    public async Task StartServerAsync(WebApplication host, string folder, CancellationToken token)
    {
        string normalizedFolder = Path.GetFullPath(folder);
        var contentService = new FileSystemContentService(normalizedFolder, _serializer, _plugins);
        OmnyManifest manifest = await contentService.GetManifestAsync();
        string outputSetting = manifest.BuildConfiguration?.OutputDirectory ?? "dist";
        string outputDirectory = Path.GetFullPath(Path.Combine(normalizedFolder, 
            outputSetting));
        PhysicalFileProvider fileProvider = new(outputDirectory);
        var staticFileOptions = new StaticFileOptions
        {
            FileProvider = fileProvider,
            RequestPath = ""
        };
        
        DefaultFilesOptions defaultFileOptions = new()
        {
            FileProvider = fileProvider,
            DefaultFileNames = { "index.html" },
            RequestPath = ""
        };

        host.UseDefaultFiles(defaultFileOptions);
        host.UseStaticFiles(staticFileOptions);
        await host.StartAsync(token);
        // log the URLs
        var serverAddresses = host.Urls;
        foreach (var address in serverAddresses)
        {
            _logger.LogInformation("Server running at {Address}", address);
        }
            
    }

    public async Task WatchAsync(string folder, CancellationToken token)
    {
        string normalizedFolder = Path.GetFullPath(folder);
        var contentService = new FileSystemContentService(normalizedFolder, _serializer, _plugins);
        OmnyManifest manifest = await contentService.GetManifestAsync();
        string outputSetting = manifest.BuildConfiguration?.OutputDirectory ?? "dist";
        string outputDirectory = Path.GetFullPath(Path.Combine(normalizedFolder, outputSetting));

        using var buildGate = new SemaphoreSlim(1, 1);
        int pendingBuild = 0;

        bool IsOutputPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            string fullPath = Path.GetFullPath(path);
            if (string.Equals(fullPath, outputDirectory, GetPathComparison()))
            {
                return true;
            }

            string outputPrefix = outputDirectory.EndsWith(Path.DirectorySeparatorChar)
                ? outputDirectory
                : outputDirectory + Path.DirectorySeparatorChar;
            return fullPath.StartsWith(outputPrefix, GetPathComparison());
        }

        async Task TriggerBuildAsync(string changedPath)
        {
            if (IsOutputPath(changedPath))
            {
                _logger.LogDebug("Ignoring watch event under output directory: {Path}", changedPath);
                return;
            }

            if (!await buildGate.WaitAsync(0, token))
            {
                Interlocked.Exchange(ref pendingBuild, 1);
                _logger.LogDebug("Build already in progress; queued another rebuild.");
                return;
            }

            try
            {
                do
                {
                    Interlocked.Exchange(ref pendingBuild, 0);
                    await BuildAsync(normalizedFolder);
                }
                while (Interlocked.CompareExchange(ref pendingBuild, 0, 1) == 1 && !token.IsCancellationRequested);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Build failed while watching.");
            }
            finally
            {
                buildGate.Release();
            }
        }

        using var watcher = new FileSystemWatcher(normalizedFolder)
        {
            IncludeSubdirectories = true,
            EnableRaisingEvents = true
        };
        watcher.Changed += async (_, e) => await TriggerBuildAsync(e.FullPath);
        watcher.Created += async (_, e) => await TriggerBuildAsync(e.FullPath);
        watcher.Deleted += async (_, e) => await TriggerBuildAsync(e.FullPath);
        watcher.Renamed += async (_, e) => await TriggerBuildAsync(e.FullPath);
        try
        {
            await Task.Delay(Timeout.Infinite, token);
        }
        catch (TaskCanceledException) { }
    }

    private static StringComparison GetPathComparison()
    {
        if (OperatingSystem.IsWindows() || OperatingSystem.IsMacOS())
        {
            return StringComparison.OrdinalIgnoreCase;
        }

        return StringComparison.Ordinal;
    }

    private static void CopyDirectory(string sourceDir, string destDir, ILogger logger)
    {
        Directory.CreateDirectory(destDir);
        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(sourceDir, file);
            var dest = Path.Combine(destDir, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            logger.LogDebug("Copying file {Source} to {Destination}", file, dest);
            File.Copy(file, dest, true);
        }
    }
}
