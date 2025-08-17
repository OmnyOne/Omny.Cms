using Omny.Cms.Manifest;

namespace Omny.Cms.Editor;

using System.Collections.Generic;

public interface IEditorService : IContentService
{
    Task<ContentItem> CreateContentItemAsync(string contentTypeName, string name);

    Task SaveContentItemAsync(string contentTypeName, ContentItem item, IDictionary<string, object> fieldContents);

    Task RenameContentItemAsync(string contentTypeName, ContentItem item, string newName);

    Task DeleteContentItemAsync(string contentTypeName, ContentItem item);

    /// <summary>
    /// Gets the appropriate editor plugin type for the given file path
    /// </summary>
    /// <param name="filePath">The file path to determine editor for</param>
    /// <returns>The editor plugin type that should handle this file</returns>
    Type GetEditorPluginType(string filePath);

    Task<string> UploadImageAsync(string fileName, byte[] data);

    /// <summary>
    /// Retrieves the name of the field that determines the folder for the given content type, if any.
    /// </summary>
    Task<string?> GetFolderFieldAsync(string contentTypeName);
}
