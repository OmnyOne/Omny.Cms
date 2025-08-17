namespace Omny.Cms.Editor.ContentTypes;

using Omny.Cms.Manifest;

public interface IContentTypeSerializerPlugin : IContentTypeSerializer
{
    string ContentType { get; }
}

public interface IContentTypeSerializer
{
    Task<List<ContentItem>> GetContentItemsAsync(
        List<string> fileNames,
        string contentType,
        OmnyManifest manifest,
        Omny.Cms.Files.IFileSystem fileSystem);
    Task<Dictionary<string, string>> WriteAsync(ContentItem item,
        IDictionary<string, object> fieldContents,
        OmnyManifest manifest,
        IEnumerable<ContentItem> parentItems);

    Task<ContentItem?> ReadAsync(
        string contentType,
        string folderPath,
        OmnyManifest manifest,
        Omny.Cms.Files.IFileSystem fileSystem);

    string GetContentItemFolderName(string defaultName, ContentTypeMetadata meta,
        IDictionary<string, object>? fieldContents = null);
    string GetContentItemFolderName(ContentItem item, ContentTypeMetadata meta,
        IDictionary<string, object>? fieldContents = null);
}
