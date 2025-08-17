# Editor Plugin System

This document describes the new flexible editor system implemented in the Omny CMS.

## Overview

The editor has been refactored from a hardcoded Markdown-only editor to a flexible plugin-based system that can handle different file types with appropriate editors.

## Architecture

### Core Interfaces

- **`IFieldPlugin`**: Unified interface that all editor and field plugins implement
- **`IPluginRegistry`**: Service for managing and retrieving plugins
- **`IEditorService`**: Extended to include `GetEditorPluginType()` method

### Included Plugins

1. **MarkdownEditorPlugin**
   - Handles: `.md`, `.markdown` files
   - Uses: Native HTML textarea with markdown styling
   - Original functionality preserved

2. **TinyMceHtmlEditorPlugin**
   - Handles: `.html`, `.htm` files
   - Uses: TinyMCE rich text editor
   - Provides WYSIWYG HTML editing

3. **TextEditorPlugin**
   - Handles: `.txt`, `.json`, `.xml`, `.css`, `.js` files
   - Uses: Native HTML textarea with monospace font
   - Fallback for unknown file types

## How It Works

1. When a user selects a content item, the `Editor.razor` component:
   - Determines the file path
   - Calls `EditorService.GetEditorPluginType()` to get the appropriate plugin type
   - Retrieves the plugin instance from `IPluginRegistry`
   - Renders the plugin's editor component

2. Each plugin provides:
   - File extension support detection
   - A `RenderFragment` that creates the appropriate editor UI
   - Content change handling

## Benefits

- **Extensible**: New editor types can be added by creating new plugins
- **Maintainable**: Each editor type is isolated in its own plugin
- **Flexible**: Different files automatically get the most appropriate editor
- **Backward Compatible**: Existing Markdown functionality is preserved

## Adding New Plugins

To add a new editor plugin:

1. Create a class implementing `IFieldPlugin`
2. Register it in `Program.cs` DI container
3. Update `ManifestEditorService.GetEditorPluginType()` to map file extensions to your plugin
4. Add the plugin type to `PluginRegistry.GetAllEditorPlugins()`

## Example Usage

```csharp
// Example of adding a custom YAML editor plugin
public class YamlEditorPlugin : IFieldPlugin
{
    public string FieldType => "YAML";
    public string[] SupportedExtensions => [".yml", ".yaml"];
    
    public bool CanHandle(string fileExtension) =>
        SupportedExtensions.Contains(fileExtension, StringComparer.OrdinalIgnoreCase);
    
    public RenderFragment RenderEditor(object? value, EventCallback<object?> onContentChanged)
    {
        // Return custom YAML editor component
    }
}
```