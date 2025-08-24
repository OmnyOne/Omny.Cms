using System.Text.Json;
using System.Text.Json.Serialization;
using System.Linq;
using Omny.Cms.Manifest;
using Omny.Cms.Editor;
using Omny.Cms.UiRepositories.Files;
using Omny.Cms.UiRepositories.Files.GitHub;
using Omny.Cms.UiImages.Services;
using Omny.Cms.Editor.ContentTypes;
using Omny.Cms.Files;
using Omny.Cms.Abstractions.Manifest;
using Omny.Cms.Core.Editor;
using System.Linq;

namespace Omny.Cms.Editor;

public class RemoteContentService(
    IRemoteFileService remoteFileService,
    IImageStorageService imageStorage,
    IEnumerable<IContentTypePlugin> plugins,
    IContentTypeSerializer serializer,
    IManifestProvider manifestProvider,
    OmnyManifest? manifestOverride = null,
    IGitHubClientProvider? gitHubClientProvider = null)
    : IContentService
{
    private readonly IRemoteFileService _remoteFileService = remoteFileService;
    private readonly IImageStorageService _imageStorage = imageStorage;
    private readonly IEnumerable<IContentTypePlugin> _plugins = plugins;
    private readonly IContentTypeSerializer _serializer = serializer;
    private readonly IGitHubClientProvider? _gitHubClientProvider = gitHubClientProvider;
    private readonly IManifestProvider _manifestProvider = manifestProvider;

    public async Task<OmnyManifest> GetManifestAsync()
    {
        if (manifestOverride is not null)
        {
            if (_manifestProvider is ManifestProvider mp)
            {
                mp.Manifest = manifestOverride;
            }
            return manifestOverride;
        }

        if (_manifestProvider.Manifest is not null)
        {
            return _manifestProvider.Manifest;
        }

        var manifestContent = await _remoteFileService.GetFileContentsAsync(OmnyManifest.ManifestFileName);
        string? manifestJson = manifestContent.Contents;
        var manifest = ManifestLoader.LoadManifest(manifestJson, _plugins);
        if (_manifestProvider is ManifestProvider mp2)
        {
            mp2.Manifest = manifest;
        }
        return manifest;
    }

    public async Task<IEnumerable<ContentType>> GetContentTypesAsync()
    {
        OmnyManifest manifest = await GetManifestAsync();
        return manifest.ContentTypeDefinitions.Select(kvp => new ContentType(kvp.Key, kvp.Value.Icon));
    }

    public async Task<ContentItem> RefreshContentItemAsync(ContentItem item)
    {
        OmnyManifest manifest = await GetManifestAsync();
        if (!manifest.ContentTypeDefinitions.TryGetValue(item.ContentType, out var metadata))
        {
            throw new InvalidOperationException("Unknown content type");
        }
        if (_gitHubClientProvider != null)
        {
            await _gitHubClientProvider.GetBranchShaAsync(true);
        }
        if (metadata.Fields is { Length: > 0 } && metadata.Folder is not null)
        {
            var fs = new RemoteFileSystem(_remoteFileService);
            string folderName = _serializer.GetContentItemFolderName(item, metadata, item.FieldValues);
            string folderPath = Path.Combine(metadata.Folder, folderName).Replace("\\", "/");
            var result = await _serializer.ReadAsync(item.ContentType, folderPath, manifest, fs);
            return result!;
        }
        else if (metadata.FileExtensionToIdMapping is not null)
        {
            string? filePath = item.FilePaths.FirstOrDefault();
            if (filePath != null)
            {
                var fs = new RemoteFileSystem(_remoteFileService);
                var result = await _serializer.ReadAsync(item.ContentType, filePath, manifest, fs);
                return result!;
            }
        }

        return item;
    }

    public async Task<IEnumerable<FieldDefinition>> GetFieldDefinitionsAsync(string contentTypeName)
    {
        OmnyManifest manifest = await GetManifestAsync();
        if (manifest.ContentTypeDefinitions.TryGetValue(contentTypeName, out var meta) && meta.Fields is { Length: > 0 })
        {
            return meta.Fields;
        }
        return Enumerable.Empty<FieldDefinition>();
    }

    private static string GetFieldKind(string fieldType, OmnyManifest manifest)
    {
        if (manifest.FieldTypeDefinitions != null && manifest.FieldTypeDefinitions.TryGetValue(fieldType, out var def))
        {
            return def.Type;
        }
        return fieldType;
    }

    public async Task<string> GetFieldKindAsync(string fieldTypeName)
    {
        OmnyManifest manifest = await GetManifestAsync();
        return GetFieldKind(fieldTypeName, manifest);
    }

    public async Task<FieldTypeDefinition?> GetFieldTypeDefinitionAsync(string fieldTypeName)
    {
        OmnyManifest manifest = await GetManifestAsync();
        if (manifest.FieldTypeDefinitions != null && manifest.FieldTypeDefinitions.TryGetValue(fieldTypeName, out var def))
        {
            return def;
        }
        return null;
    }

    public async Task<IEnumerable<ContentItem>> GetContentItemsAsync(string contentTypeName)
    {
        OmnyManifest manifest = await GetManifestAsync();
        if (!manifest.ContentTypeDefinitions.TryGetValue(contentTypeName, out _))
        {
            return Enumerable.Empty<ContentItem>();
        }

        var files = await _remoteFileService.GetFilesAsync();
        var fileNames = files.Select(f => f.Path).ToList();
        var fs = new RemoteFileSystem(_remoteFileService);
        return await _serializer.GetContentItemsAsync(fileNames, contentTypeName, manifest, fs);
    }

    public async Task<IEnumerable<string>> GetImagesAsync()
    {
        OmnyManifest manifest = await GetManifestAsync();
        string folder = manifest.ImageLocation ?? "content/images";

        var storageImages = await _imageStorage.ListImagesAsync(folder);
        if (storageImages.Any())
        {
            return storageImages;
        }

        var files = await _remoteFileService.GetFilesAsync();
        return files.Where(f => f.Path.StartsWith(folder, StringComparison.OrdinalIgnoreCase))
            .Select(f => Path.GetFileName(f.Path));
    }

    public async Task<string?> GetImageUrlAsync(string fileName)
    {
        OmnyManifest manifest = await GetManifestAsync();
        string folder = manifest.ImageLocation ?? "content/images";
        string path = Path.Combine(folder, fileName).Replace("\\", "/");
        var url = await _imageStorage.GetPublicUrlAsync(path);
        if (url != path)
        {
            return url;
        }

        var contents = await _remoteFileService.GetFileContentsAsync(path);
        if (contents.Contents is null)
        {
            return null;
        }
        string mime = Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".svg" => "image/svg+xml",
            _ => "application/octet-stream"
        };
        return $"data:{mime};base64,{contents.Contents}";
    }

    public async Task<bool> UseRandomImageNamesAsync()
    {
        OmnyManifest manifest = await GetManifestAsync();
        return manifest.RandomImageNames;
    }

    public string GetHtmlEditor()
    {
        OmnyManifest? manifest = _manifestProvider.Manifest ?? manifestOverride;
        if (manifest is null)
        {
            return "quill";
        }
        return string.IsNullOrWhiteSpace(manifest.HtmlEditor) ? "tinymce" : manifest.HtmlEditor;
    }

    public async Task<int> GetMaxImageSizeKbAsync()
    {
        OmnyManifest manifest = await GetManifestAsync();
        return manifest.MaxImageSizeKb > 0 ? manifest.MaxImageSizeKb : 1024;
    }

    public async Task<object?> GetCustomDataAsync(string key)
    {
        OmnyManifest manifest = await GetManifestAsync();
        return manifest.CustomData.TryGetValue(key, out var val) ? val : null;
    }
}
