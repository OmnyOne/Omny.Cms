using System.Text.Json;
using System.IO;
using System.Linq;
using Omny.Cms.Editor.ContentTypes;
using Omny.Cms.Editor.Fields;
using Omny.Cms.Files;
using Omny.Cms.Manifest;

namespace Omny.Cms.Editor;

public class DefaultContentTypeSerializer(IEnumerable<IFieldPlugin> fieldPlugins) : IContentTypeSerializerPlugin
{
    public string ContentType => string.Empty; // handles all types

    private static string GetFieldKind(string fieldType, OmnyManifest manifest)
    {
        if (manifest.FieldTypeDefinitions != null && manifest.FieldTypeDefinitions.TryGetValue(fieldType, out var def))
        {
            return def.Type;
        }
        return fieldType;
    }

    private static string GetDefaultExtension(string fieldType) => fieldType.ToLowerInvariant() switch
    {
        "markdown" => ".md",
        "html" => ".html",
        "imagetext" => ".json",
        "menuitem" => ".json",
        _ => ".txt"
    };

    public async Task<List<ContentItem>> GetContentItemsAsync(List<string> fileNames, string contentType, OmnyManifest manifest, IFileSystem fileSystem)
    {
        if (!manifest.ContentTypeDefinitions.TryGetValue(contentType, out var metadata) || metadata.Folder is null)
        {
            throw new InvalidOperationException($"Unknown content type {contentType}");
        }
        var directories = fileNames
            .Where(f => f.StartsWith(metadata.Folder, StringComparison.OrdinalIgnoreCase))
            .Select(f => Path.GetDirectoryName(f)!)
            .Where(d => !string.IsNullOrEmpty(d))
            .Distinct();

        List<ContentItem> items = new();
        foreach (var dir in directories)
        {
            var item = await ReadAsync(contentType, dir, manifest, fileSystem);
            if (item != null)
            {
                items.Add(item);
            }
        }

        return items;
    }

    public Task<Dictionary<string, string>> WriteAsync(ContentItem item,
        IDictionary<string, object> fieldContents,
        OmnyManifest manifest,
        IEnumerable<ContentItem> parentItems)
    {
        if (!manifest.ContentTypeDefinitions.TryGetValue(item.ContentType, out var meta)
            || meta.Fields is not { Length: > 0 }
            || meta.Folder is null)
        {
            throw new InvalidOperationException($"Unknown content type {item.ContentType}");
        }

        var fields = meta.Fields;
        var folderName = GetContentItemFolderName(item, meta, fieldContents);

        var folder = Path.Combine(meta.Folder, folderName).Replace("\\", "/");
        var files = new Dictionary<string, string>();
        var fieldValues = new Dictionary<string, object?>();

        foreach (var field in fields)
        {
            string kind = GetFieldKind(field.FieldType, manifest);
            if (kind == "collection" && fieldContents.TryGetValue(field.Name, out var collObj) && collObj is CollectionFieldContent coll)
            {
                List<object> entries = new();
                for (int i = 0; i < coll.Items.Count; i++)
                {
                    var entry = coll.Items[i];
                    string entryKind = GetFieldKind(entry.FieldType, manifest);
                    if (entryKind == "collection" || entryKind == "compound" || GetDefaultExtension(entry.FieldType).Equals(".json", StringComparison.OrdinalIgnoreCase))
                    {
                        object? parsed = entry.Content;
                        if (entry.Content is string s)
                        {
                            try
                            {
                                parsed = JsonSerializer.Deserialize<object>(s);
                            }
                            catch
                            {
                                parsed = s;
                            }
                        }
                        entries.Add(new Dictionary<string, object?> { ["type"] = entry.FieldType, ["value"] = parsed });
                    }
                    else
                    {
                        string fileName = $"{field.Name}-{i + 1}{GetDefaultExtension(entry.FieldType)}";
                        string filePath = Path.Combine(folder, fileName).Replace("\\", "/");
                        files[filePath] = entry.Content;
                        entries.Add(new Dictionary<string, object?> { ["type"] = entry.FieldType, ["file"] = fileName });
                    }
                }
                fieldValues[field.Name] = entries;
            }
            else if ((kind == "compound" || (field.Extension != null && field.Extension.Equals(".json", StringComparison.OrdinalIgnoreCase))) && fieldContents.TryGetValue(field.Name, out var compObj) && compObj is string compStr)
            {
                object? parsed = compStr;
                try
                {
                    parsed = JsonSerializer.Deserialize<object>(compStr);
                }
                catch { }
                fieldValues[field.Name] = parsed;
            }
            else if (field.Extension is not null && !field.Extension.Equals(".json", StringComparison.OrdinalIgnoreCase))
            {
                string fileName = $"{field.Name}{field.Extension}";
                string targetPath = Path.Combine(folder, fileName).Replace("\\", "/");
                if (fieldContents.TryGetValue(field.Name, out var contentObj) && contentObj is string contentStr)
                {
                    files[targetPath] = contentStr;
                }
                fieldValues[field.Name] = fileName;
            }
            else if (fieldContents.TryGetValue(field.Name, out var valueObj))
            {
                fieldValues[field.Name] = valueObj;
            }
        }

        var data = new ContentItemData
        {
            ContentType = item.ContentType,
            Name = item.Name,
            FieldValues = fieldValues
        };

        string fieldsPath = Path.Combine(folder, "fields.json").Replace("\\", "/");
        files[fieldsPath] = JsonSerializer.Serialize(data);

        return Task.FromResult(files);
    }

    public string GetContentItemFolderName(string defaultName, ContentTypeMetadata meta, IDictionary<string, object>? fieldContents = null)
    {
        string folderName = defaultName;
        if (!string.IsNullOrEmpty(meta.FolderField))
        {
            if (fieldContents is not null && fieldContents.TryGetValue(meta.FolderField, out var folderValObj) && folderValObj is string folderVal)
            {
                folderName = folderVal;
            }
        }

        return folderName;
    }

    public string GetContentItemFolderName(ContentItem item, ContentTypeMetadata meta, IDictionary<string, object>? fieldContents = null)
    {
        string folderName = item.Name;
        if (!string.IsNullOrEmpty(meta.FolderField))
        {
            if (fieldContents is not null && fieldContents.TryGetValue(meta.FolderField, out var folderValObj) && folderValObj is string folderVal)
            {
                folderName = folderVal;
            }
            else if (item.FieldValues != null && item.FieldValues.TryGetValue(meta.FolderField, out var existing))
            {
                if (existing is JsonElement je && je.ValueKind == JsonValueKind.String)
                    folderName = je.GetString() ?? folderName;
                else if (existing is string s)
                    folderName = s;
            }
        }

        return folderName;
    }

    public async Task<ContentItem?> ReadAsync(string contentType, string folderPath, OmnyManifest manifest, Omny.Cms.Files.IFileSystem fileSystem)
    {
        if (!manifest.ContentTypeDefinitions.TryGetValue(contentType, out var meta) || meta.Fields is not { Length: > 0 } || meta.Folder is null)
        {
            return null;
        }

        string jsonPath = Path.Combine(folderPath, "fields.json").Replace("\\", "/");
        if (!await fileSystem.FileExistsAsync(jsonPath))
            return null;

        ContentItemData? data;
        try
        {
            var json = await fileSystem.ReadAllTextAsync(jsonPath);
            data = JsonSerializer.Deserialize<ContentItemData>(json);
        }
        catch
        {
            return null;
        }

        if (data == null)
            return null;

        List<string> filePaths = new();
        if (meta.Fields != null)
        {
            foreach (var field in meta.Fields)
            {
                if (field.Extension is not null && !field.Extension.Equals(".json", StringComparison.OrdinalIgnoreCase))
                {
                    string fp = Path.Combine(folderPath, $"{field.Name}{field.Extension}").Replace("\\", "/");
                    if (await fileSystem.FileExistsAsync(fp))
                    {
                        if(data.FieldValues is not null) {
                            data.FieldValues[field.Name] = await fileSystem.ReadAllTextAsync(fp);
                        }
                        filePaths.Add(fp);
                    }
                }
            }
        }

        var item = new ContentItem(contentType, data.Name, filePaths.ToArray())
        {
            FieldValues = data.FieldValues,
            IsNew = false,
            OriginalFilePaths = filePaths.Append(jsonPath).ToArray(),
            OriginalFolderPath = folderPath
        };
        return item;
    }
}

