using System.Text;
using Omny.Cms.Editor.ContentTypes;
using Omny.Cms.Manifest;
using Omny.Cms.Editor;
using Omny.Cms.Files;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Omny.Cms.Plugins.Hexo;

public class HexoPostSerializer(DefaultContentTypeSerializer defaultSerializer) : IContentTypeSerializerPlugin
{
    public string ContentType => "Hexo.Post";

    public Task<string> GetFieldKindAsync(string fieldTypeName) => Task.FromResult(fieldTypeName); // not used

    public string GetContentItemFolderName(string defaultName, ContentTypeMetadata meta, IDictionary<string, object>? fieldContents = null)
    {
        return defaultName;
    }

    public string GetContentItemFolderName(ContentItem item, ContentTypeMetadata meta, IDictionary<string, object>? fieldContents = null)
    {
        return item.Name;
    }

    public async Task<List<ContentItem>> GetContentItemsAsync(List<string> fileNames, string contentType, OmnyManifest manifest, IFileSystem fileSystem)
    {
        if (!manifest.ContentTypeDefinitions.TryGetValue(contentType, out var meta) || meta.FileExtensionToIdMapping == null)
        {
            return await defaultSerializer.GetContentItemsAsync(fileNames, contentType, manifest, fileSystem);
        }
        if (!string.Equals(meta.PluginType, ContentType, StringComparison.OrdinalIgnoreCase))
        {
            return await defaultSerializer.GetContentItemsAsync(fileNames, contentType, manifest, fileSystem);
        }

        var items = new List<ContentItem>();
        foreach (var file in fileNames.Where(f => f.StartsWith(meta.Folder!, StringComparison.OrdinalIgnoreCase)))
        {
            if (Path.GetExtension(file).Equals(".md", StringComparison.OrdinalIgnoreCase))
            {
                var item = await ReadAsync(contentType, file, manifest, fileSystem);
                if (item != null)
                {
                    items.Add(item);
                }
            }
        }
        return items;
    }

    public async Task<Dictionary<string, string>> WriteAsync(ContentItem item, IDictionary<string, object> fieldContents, OmnyManifest manifest, IEnumerable<ContentItem> parentItems)
    {
        if (!manifest.ContentTypeDefinitions.TryGetValue(item.ContentType, out var meta) || meta.FileExtensionToIdMapping == null)
        {
            return await defaultSerializer.WriteAsync(item, fieldContents, manifest, parentItems);
        }
        if (!string.Equals(meta.PluginType, ContentType, StringComparison.OrdinalIgnoreCase))
        {
            return await defaultSerializer.WriteAsync(item, fieldContents, manifest, parentItems);
        }

        var serializer = new SerializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).Build();
        Dictionary<string, object?> fm = new();
        foreach (var field in meta.Fields ?? Array.Empty<FieldDefinition>())
        {
            if (string.Equals(field.Name, "Body", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (fieldContents.TryGetValue(field.Name, out var val))
            {
                fm[field.Name] = val;
            }
        }

        var yaml = serializer.Serialize(fm).Trim();
        string body = fieldContents.TryGetValue("Body", out var bodyObj) ? bodyObj?.ToString() ?? string.Empty : string.Empty;

        var sb = new StringBuilder();
        sb.AppendLine("---");
        sb.AppendLine(yaml);
        sb.AppendLine("---");
        sb.AppendLine();
        sb.Append(body);

        string path = Path.Combine(meta.Folder!, item.Name + ".md").Replace("\\", "/");
        return new Dictionary<string, string> { [path] = sb.ToString() };
    }

    public async Task<ContentItem?> ReadAsync(string contentType, string filePath, OmnyManifest manifest, Omny.Cms.Files.IFileSystem fileSystem)
    {
        if (!manifest.ContentTypeDefinitions.TryGetValue(contentType, out var meta) || meta.FileExtensionToIdMapping == null)
        {
            return await defaultSerializer.ReadAsync(contentType, filePath, manifest, fileSystem);;
        }
        if (!string.Equals(meta.PluginType, ContentType, StringComparison.OrdinalIgnoreCase))
        {
            return await defaultSerializer.ReadAsync(contentType, filePath, manifest, fileSystem);
        }
        
        if(!filePath.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
           filePath += ".md";
        }

        if (!await fileSystem.FileExistsAsync(filePath))
        {
            return null;
        }

        string text = await fileSystem.ReadAllTextAsync(filePath);
        string front = string.Empty;
        string body = text;
        if (text.StartsWith("---"))
        {
            int end = text.IndexOf("\n---", 3, StringComparison.Ordinal);
            if (end >= 0)
            {
                front = text.Substring(3, end - 3).Trim('\r', '\n');
                body = text.Substring(end + 4).TrimStart('\r', '\n');
            }
        }

        var deserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
        var fm = string.IsNullOrWhiteSpace(front) ? new Dictionary<string, object>() : deserializer.Deserialize<Dictionary<string, object>>(front);
        fm["Body"] = body;
        string name = Path.GetFileNameWithoutExtension(filePath);

        var item = new ContentItem(contentType, name, new[] { filePath })
        {
            FieldValues = fm,
            IsNew = false,
            OriginalFilePaths = new[] { filePath },
            OriginalFolderPath = Path.GetDirectoryName(filePath)
        };
        return item;
    }
}
