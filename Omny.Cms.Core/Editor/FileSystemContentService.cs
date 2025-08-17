using System.Text.Json;
using Omny.Cms.Manifest;
using Omny.Cms.Editor.ContentTypes;
using System.Linq;
using System.Threading.Tasks;
using Omny.Cms.Files;

namespace Omny.Cms.Editor;

public class FileSystemContentService : IContentService
{
    private readonly string _folder;
    private OmnyManifest? _manifest;
    private readonly IEnumerable<IContentTypePlugin> _plugins;
    private readonly IContentTypeSerializer _serializer;

    public FileSystemContentService(
        string folder, 
        IContentTypeSerializer serializer,
        IEnumerable<IContentTypePlugin>? plugins = null
        )
    {
        _folder = folder;
        _plugins = plugins ?? Enumerable.Empty<IContentTypePlugin>();
        _serializer = serializer;
    }

    private class LocalFs : Omny.Cms.Files.IFileSystem
    {
        public Task<bool> FileExistsAsync(string path) => Task.FromResult(System.IO.File.Exists(path));
        public Task<string> ReadAllTextAsync(string path) => System.IO.File.ReadAllTextAsync(path);
    }

    public async Task<OmnyManifest> GetManifestAsync()
    {
        if (_manifest != null) return _manifest;
        string path = Path.Combine(_folder, OmnyManifest.ManifestFileName);
        string? json = File.Exists(path) ? await File.ReadAllTextAsync(path) : null;
        _manifest = ManifestLoader.LoadManifest(json, _plugins);
        return _manifest;
    }

    public async Task<IEnumerable<ContentType>> GetContentTypesAsync()
    {
        var manifest = await GetManifestAsync();
        return manifest.ContentTypeDefinitions.Select(kv => new ContentType(kv.Key, kv.Value.Icon));
    }

    public async Task<IEnumerable<FieldDefinition>> GetFieldDefinitionsAsync(string contentTypeName)
    {
        var manifest = await GetManifestAsync();
        if (manifest.ContentTypeDefinitions.TryGetValue(contentTypeName, out var meta) && meta.Fields is { Length: >0 })
        {
            return meta.Fields;
        }
        return Enumerable.Empty<FieldDefinition>();
    }
    
    public async Task<ContentItem> RefreshContentItemAsync(ContentItem item)
    {
        var manifest = await GetManifestAsync();
        if (!manifest.ContentTypeDefinitions.TryGetValue(item.ContentType, out var metadata))
        {
            throw new InvalidOperationException("Unknown content type");
        }

        if (metadata.Fields is { Length: > 0 } && metadata.Folder is not null)
        {
            string folder = Path.Combine(_folder, metadata.Folder, item.Name);
            var result = await _serializer.ReadAsync(item.ContentType, folder, manifest, new LocalFs());
            return result!;
        }
        else if (metadata.FileExtensionToIdMapping != null)
        {
            string? filePath = item.FilePaths.FirstOrDefault();
            if (filePath != null)
            {
                var result = await _serializer.ReadAsync(item.ContentType, filePath, manifest, new LocalFs());
                return result!;
            }
        }
        return item;
    }

    public async Task<IEnumerable<ContentItem>> GetContentItemsAsync(string contentTypeName)
    {
        var manifest = await GetManifestAsync();
        if (!manifest.ContentTypeDefinitions.TryGetValue(contentTypeName, out var metadata))
        {
            return Enumerable.Empty<ContentItem>();
        }

        if (metadata.Fields is { Length: > 0 } && metadata.Folder is not null)
        {
            string ctFolder = Path.Combine(_folder, metadata.Folder);
            if (!Directory.Exists(ctFolder))
            {
                return new List<ContentItem>();
            }

            var tasks = Directory.GetDirectories(ctFolder)
                .Select(dir => _serializer.ReadAsync(contentTypeName, dir, manifest, new LocalFs()))
                .ToArray();

            var results = await Task.WhenAll(tasks);
            return results.Where(r => r != null).Cast<ContentItem>().ToList();
        }
        else if (metadata.FileExtensionToIdMapping != null)
        {
            var tasks = new List<Task<ContentItem?>>();
            foreach (var mapping in metadata.FileExtensionToIdMapping)
            {
                string dir = Path.Combine(_folder, mapping.Value);
                if (!Directory.Exists(dir))
                {
                    continue;
                }

                foreach (var file in Directory.GetFiles(dir, $"*{mapping.Key}", SearchOption.TopDirectoryOnly))
                {
                    tasks.Add(_serializer.ReadAsync(contentTypeName, file, manifest, new LocalFs()));
                }
            }

            var results = await Task.WhenAll(tasks);
            return results.Where(r => r != null).Cast<ContentItem>().ToList();
        }
        return Enumerable.Empty<ContentItem>();
    }

    public async Task<IEnumerable<string>> GetImagesAsync()
    {
        var manifest = await GetManifestAsync();
        string folder = manifest.ImageLocation ?? "content/images";
        string path = Path.Combine(_folder, folder);
        if (Directory.Exists(path))
        {
            return Directory.GetFiles(path)
                .Select(f => Path.GetFileName(f)!)
                .Where(n => n is not null);
        }
        return Enumerable.Empty<string>();
    }

    public Task<string?> GetImageUrlAsync(string fileName)
    {
        string path = Path.Combine(_folder, fileName);
        if (!File.Exists(path))
            return Task.FromResult<string?>(null);
        string mime = Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".svg" => "image/svg+xml",
            _ => "application/octet-stream"
        };
        string data = Convert.ToBase64String(File.ReadAllBytes(path));
        return Task.FromResult<string?>(
            $"data:{mime};base64,{data}");
    }

    public async Task<bool> UseRandomImageNamesAsync()
    {
        var manifest = await GetManifestAsync();
        return manifest.RandomImageNames;
    }

    public string GetHtmlEditor()
    {
        if (_manifest == null)
            return "tinymce";
        return string.IsNullOrWhiteSpace(_manifest.HtmlEditor) ? "tinymce" : _manifest.HtmlEditor;
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
        var manifest = await GetManifestAsync();
        return GetFieldKind(fieldTypeName, manifest);
    }

    public async Task<FieldTypeDefinition?> GetFieldTypeDefinitionAsync(string fieldTypeName)
    {
        var manifest = await GetManifestAsync();
        if (manifest.FieldTypeDefinitions != null && manifest.FieldTypeDefinitions.TryGetValue(fieldTypeName, out var def))
        {
            return def;
        }
        return null;
    }

    public async Task<int> GetMaxImageSizeKbAsync()
    {
        var manifest = await GetManifestAsync();
        return manifest.MaxImageSizeKb > 0 ? manifest.MaxImageSizeKb : 1024;
    }

    public async Task<object?> GetCustomDataAsync(string key)
    {
        var manifest = await GetManifestAsync();
        return manifest.CustomData.TryGetValue(key, out var val) ? val : null;
    }
}
