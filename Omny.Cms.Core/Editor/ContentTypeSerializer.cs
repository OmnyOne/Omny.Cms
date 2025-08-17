using Omny.Cms.Editor.ContentTypes;
using Omny.Cms.Files;
using Omny.Cms.Manifest;

namespace Omny.Cms.Editor;

public class ContentTypeSerializer(IEnumerable<IContentTypeSerializerPlugin> plugins) : IContentTypeSerializer
{
    private IContentTypeSerializerPlugin GetAppropriateSerializer(string? pluginType)
    {
        var serializer = plugins.FirstOrDefault(p => p.ContentType.Equals(pluginType, StringComparison.OrdinalIgnoreCase));
        if (serializer == null)
        {
            return plugins.Single(s => s is DefaultContentTypeSerializer);
        }
        return serializer;
    }

    public Task<List<ContentItem>> GetContentItemsAsync(List<string> fileNames,string contentType, OmnyManifest manifest, IFileSystem fileSystem)
    {
        if (!manifest.ContentTypeDefinitions.TryGetValue(contentType, out var meta) || meta.Folder is null)
        {
            throw new InvalidOperationException($"Unknown content type {contentType}");
        }

        var serializer = GetAppropriateSerializer(meta.PluginType);
        return serializer.GetContentItemsAsync(fileNames, contentType, manifest, fileSystem);
    }

    public Task<Dictionary<string, string>> WriteAsync(ContentItem item, IDictionary<string, object> fieldContents, OmnyManifest manifest, IEnumerable<ContentItem> parentItems)
    {
        if (!manifest.ContentTypeDefinitions.TryGetValue(item.ContentType, out var meta) || meta.Folder is null)
        {
            throw new InvalidOperationException($"Unknown content type {item.ContentType}");
        }

        var serializer = GetAppropriateSerializer(meta.PluginType);
        return serializer.WriteAsync(item, fieldContents, manifest, parentItems);
    }

    public Task<ContentItem?> ReadAsync(string contentType, string folderPath, OmnyManifest manifest, IFileSystem fileSystem)
    {
        if (!manifest.ContentTypeDefinitions.TryGetValue(contentType, out var meta) || meta.Folder is null)
        {
            throw new InvalidOperationException($"Unknown content type {contentType}");
        }

        var serializer = GetAppropriateSerializer(meta.PluginType);
        return serializer.ReadAsync(contentType, folderPath, manifest, fileSystem);
    }

    public string GetContentItemFolderName(string defaultName, ContentTypeMetadata meta, IDictionary<string, object>? fieldContents = null)
    {
        var serializer = GetAppropriateSerializer(meta.PluginType);
        return serializer.GetContentItemFolderName(defaultName, meta, fieldContents);
    }

    public string GetContentItemFolderName(ContentItem item, ContentTypeMetadata meta, IDictionary<string, object>? fieldContents = null)
    {
        var serializer = GetAppropriateSerializer(meta.PluginType);
        return serializer.GetContentItemFolderName(item, meta, fieldContents);
    }
}