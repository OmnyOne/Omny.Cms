using System;
using System.IO;
using System.Linq;
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

    public async Task WatchAsync(string folder, CancellationToken token)
    {
        using var watcher = new FileSystemWatcher(folder)
        {
            IncludeSubdirectories = true,
            EnableRaisingEvents = true
        };
        watcher.Changed += async (_, __) => await BuildAsync(folder);
        watcher.Created += async (_, __) => await BuildAsync(folder);
        watcher.Deleted += async (_, __) => await BuildAsync(folder);
        watcher.Renamed += async (_, __) => await BuildAsync(folder);
        try
        {
            await Task.Delay(Timeout.Infinite, token);
        }
        catch (TaskCanceledException) { }
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
