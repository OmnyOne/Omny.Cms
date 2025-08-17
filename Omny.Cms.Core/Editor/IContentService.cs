using Omny.Cms.Files;
using Omny.Cms.Manifest;

namespace Omny.Cms.Editor;

public interface IContentService
{
    Task<OmnyManifest> GetManifestAsync();
    Task<IEnumerable<ContentType>> GetContentTypesAsync();

    Task<ContentItem> RefreshContentItemAsync(ContentItem item);
    Task<IEnumerable<ContentItem>> GetContentItemsAsync(string contentTypeName);
    Task<IEnumerable<FieldDefinition>> GetFieldDefinitionsAsync(string contentTypeName);
    Task<IEnumerable<string>> GetImagesAsync();
    Task<string?> GetImageUrlAsync(string fileName);
    Task<bool> UseRandomImageNamesAsync();
    string GetHtmlEditor();
    Task<string> GetFieldKindAsync(string fieldTypeName);
    Task<FieldTypeDefinition?> GetFieldTypeDefinitionAsync(string fieldTypeName);
    Task<int> GetMaxImageSizeKbAsync();
    Task<object?> GetCustomDataAsync(string key);
}
