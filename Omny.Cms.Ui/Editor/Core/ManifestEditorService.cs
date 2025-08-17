using Omny.Cms.UiRepositories.Files;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Omny.Cms.Plugins.Fields;
using Omny.Cms.Editor.Plugins;
using Omny.Cms.Manifest;
using Omny.Cms.Editor;
using Omny.Cms.Editor.ContentTypes;
using Omny.Cms.UiImages.Services;
using Omny.Cms.UiRepositories.Files.GitHub;
using Omny.Cms.Abstractions.Manifest;

namespace Omny.Cms.Editor;

public class ManifestEditorService : RemoteContentService, IEditorService
{
    private readonly IRemoteFileService _remoteFileService;
    private readonly IImageStorageService _imageStorage;
    private readonly IContentTypeSerializer _serializer;

    public ManifestEditorService(
        IRemoteFileService remoteFileService,
        IImageStorageService imageStorage,
        IEnumerable<IContentTypePlugin> contentTypePlugins,
        IContentTypeSerializer serializer,
        IManifestProvider manifestProvider,
        OmnyManifest? manifestOverride = null,
        IGitHubClientProvider? gitHubClientProvider = null)
        : base(remoteFileService, imageStorage, contentTypePlugins, serializer, manifestProvider, manifestOverride, gitHubClientProvider)
    {
        _remoteFileService = remoteFileService;
        _imageStorage = imageStorage;
        _serializer = serializer;
    }



    public record CollectionFileList(
        [property: JsonPropertyName("fields")] string[] Fields,
        [property: JsonPropertyName("types")] string[]? Types);

    public async Task<ContentItem> CreateContentItemAsync(string contentTypeName, string name)
    {
        OmnyManifest manifest = await GetManifestAsync();
        if (!manifest.ContentTypeDefinitions.TryGetValue(contentTypeName, out var metadata))
        {
            throw new InvalidOperationException("Unknown content type");
        }

        if (metadata.Fields is { Length: > 0 } && metadata.Folder is not null)
        {
            var fields = metadata.Fields;
            
            string folder = Path.Combine(metadata.Folder, name).Replace("\\", "/");
            var filePaths = new List<string>();
            var fieldValues = new Dictionary<string, object>();
            foreach (var field in fields)
            {
                if (field.Extension is not null && !field.Extension.Equals(".json", StringComparison.OrdinalIgnoreCase))
                {
                    string path = $"{field.Name}{field.Extension}".Replace("\\", "/");
                    filePaths.Add(path);
                }
                else if (await GetFieldKindAsync(field.FieldType) is "text" or "image")
                {
                    fieldValues[field.Name] = string.Empty;
                }
            }

            var allPaths = filePaths.Append("fields.json".Replace("\\", "/")).ToArray();
            string folderName = _serializer.GetContentItemFolderName(name, metadata, fieldValues);
            var item = new ContentItem(contentTypeName, name, filePaths.ToArray())
            {
                FieldValues = fieldValues,
                IsNew = true,
                OriginalFilePaths = allPaths.Select(p => Path.Combine(metadata.Folder, folderName, p).Replace("\\", "/")).ToArray(),
                OriginalFolderPath = folderName
            };
            return item;
        }
        else if (metadata.FileExtensionToIdMapping is not null)
        {
            var kvp = metadata.FileExtensionToIdMapping.First();
            string path = $"{kvp.Value}{name}{kvp.Key}";
            var originalPaths = new[] { path };
            return new ContentItem(contentTypeName, name, new[] { path })
            {
                FieldValues = new(),
                IsNew = true,
                OriginalFilePaths = originalPaths,
                OriginalFolderPath = Path.GetDirectoryName(path)
            };
        }

        throw new InvalidOperationException("Invalid manifest definition");
    }

    public async Task SaveContentItemAsync(string contentTypeName, ContentItem item,
        IDictionary<string, object> fieldContents)
    {
        OmnyManifest manifest = await GetManifestAsync();
        var parents = await GetContentItemsAsync(contentTypeName);
        if (!manifest.ContentTypeDefinitions.TryGetValue(contentTypeName, out var meta))
        {
            return;
        }
        var files = await _serializer.WriteAsync(item, fieldContents, manifest, parents);
        if (files.Count > 0)
        {
            var newPaths = files.Keys.ToArray();
            string? newFolder = Path.GetDirectoryName(newPaths.First());
            var oldPaths = item.OriginalFilePaths ?? Array.Empty<string>();

            if (!string.IsNullOrEmpty(item.OriginalFolderPath) &&
                !string.Equals(item.OriginalFolderPath, newFolder, StringComparison.OrdinalIgnoreCase))
            {
                await _remoteFileService.DeleteFolderAsync(item.OriginalFolderPath);
            }
            else
            {
                var toDelete = oldPaths.Except(newPaths, StringComparer.OrdinalIgnoreCase).ToArray();
                if (toDelete.Length > 0)
                {
                    await _remoteFileService.DeleteFilesAsync(toDelete);
                }
            }

            await _remoteFileService.WriteFilesAsync(files);
        }
    }

    public async Task RenameContentItemAsync(string contentTypeName, ContentItem item, string newName)
    {
        OmnyManifest manifest = await GetManifestAsync();
        if (!manifest.ContentTypeDefinitions.TryGetValue(contentTypeName, out var metadata))
        {
            return;
        }

        if (metadata.Fields is { Length: <= 0 } || metadata.Folder is null)
        {
            return;
        }
        
        string fieldsPath = Path.Combine(item.OriginalFolderPath ?? "not-likely-path", "fields.json").Replace("\\", "/");
        var existing = await _remoteFileService.GetFileContentsAsync(fieldsPath);
        var fields = item.FieldValues ?? new Dictionary<string, object>();
        if (!string.IsNullOrEmpty(existing.Contents) && !string.IsNullOrEmpty(item.OriginalFolderPath))
        {
            try
            {
                var data = JsonSerializer.Deserialize<ContentItemData>(existing.Contents);
                if (data != null)
                {
                    data.Name = newName;
                    fields = data.FieldValues;
                    await _remoteFileService.WriteFilesAsync(new Dictionary<string, string>
                    {
                        [fieldsPath] = JsonSerializer.Serialize(data)
                    });
                }
            }
            catch
            {
                // ignore malformed json
            }
        }

        string oldFolder = Path.Combine(item.OriginalFolderPath ?? "").Replace("\\", "/");
        string newFolderName = _serializer.GetContentItemFolderName(item, metadata, fields);
        string newFolder = Path.Combine(metadata.Folder, newFolderName).Replace("\\", "/");

        if (oldFolder != newFolder)
        {
            await _remoteFileService.RenameFolderAsync(oldFolder, newFolder);
        }


        
    }

    public async Task DeleteContentItemAsync(string contentTypeName, ContentItem item)
    {
        OmnyManifest manifest = await GetManifestAsync();
        if (!manifest.ContentTypeDefinitions.TryGetValue(contentTypeName, out var metadata))
        {
            return;
        }

        if (metadata.Fields is { Length: > 0 } && metadata.Folder is not null)
        {
            string folderName = _serializer.GetContentItemFolderName(item, metadata, item.FieldValues);
            string folder = Path.Combine(metadata.Folder, folderName).Replace("\\", "/");
            await _remoteFileService.DeleteFolderAsync(folder);
        }
    }

    public Type GetEditorPluginType(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        var htmlEditor = GetHtmlEditor();
        return extension switch
        {
            ".md" or ".markdown" => typeof(MarkdownEditorPlugin),
            ".html" or ".htm" => string.Equals(htmlEditor, "quill", StringComparison.OrdinalIgnoreCase)
                ? typeof(QuillHtmlEditorPlugin)
                : typeof(TinyMceHtmlEditorPlugin),
            ".txt" or ".json" or ".xml" or ".css" or ".js" => typeof(TextEditorPlugin),
            _ => typeof(TextEditorPlugin)
        };
    }

    public async Task<string> UploadImageAsync(string fileName, byte[] data)
    {
        OmnyManifest manifest = await GetManifestAsync();
        if (manifest.RandomImageNames)
        {
            string ext = Path.GetExtension(fileName);
            fileName = $"{Guid.NewGuid()}{ext}";
        }

        string folder = manifest.ImageLocation ?? "content/images";
        string path = Path.Combine(folder, fileName).Replace("\\", "/");
        await _remoteFileService.WriteBinaryFilesAsync(new Dictionary<string, byte[]> { [path] = data });
        await _imageStorage.UploadImageAsync(path, data);
        return fileName;
    }

    public async Task<string?> GetFolderFieldAsync(string contentTypeName)
    {
        OmnyManifest manifest = await GetManifestAsync();
        if (manifest.ContentTypeDefinitions.TryGetValue(contentTypeName, out var metadata))
        {
            return metadata.FolderField;
        }
        return null;
    }
}