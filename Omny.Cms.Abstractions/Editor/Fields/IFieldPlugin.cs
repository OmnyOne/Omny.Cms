using Microsoft.AspNetCore.Components;

namespace Omny.Cms.Editor.Fields;

/// <summary>
/// Unified plugin interface for both field and file editors.
/// </summary>
public interface IFieldPlugin
{
    /// <summary>
    /// Human friendly name that can be shown in the UI when choosing this
    /// field type.
    /// </summary>
    string DisplayName { get; }
    
    /// <summary>
    /// For the rare case like images where the data should not be fetched directly
    /// </summary>
    bool ShouldNotFetchContent => false;

    /// <summary>
    /// Optional icon (CSS class or emoji) used for buttons when adding fields
    /// of this type.
    /// </summary>
    string? Icon { get; }
    /// <summary>
    /// Logical type name for this plugin. For former editor plugins this
    /// corresponds to the previous <c>Name</c> property.
    /// </summary>
    string FieldType { get; }

    /// <summary>
    /// Optional list of file extensions this plugin can handle when editing
    /// files. Field-only plugins may return an empty array.
    /// </summary>
    string[] SupportedExtensions { get; }

    /// <summary>
    /// Default value used when a field does not have a stored value.
    /// </summary>
    object? DefaultValue { get; }

    /// <summary>
    /// Determines whether the plugin can handle the provided file extension.
    /// </summary>
    bool CanHandle(string fileExtension);

    /// <summary>
    /// Returns a fragment that renders the editing UI for the given value.
    /// </summary>
    RenderFragment RenderEditor(object? value, EventCallback<object?> onChanged);
}
