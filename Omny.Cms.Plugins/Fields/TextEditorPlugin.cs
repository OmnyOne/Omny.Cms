using Microsoft.AspNetCore.Components;
using Omny.Cms.Editor.Fields;

namespace Omny.Cms.Plugins.Fields;

public class TextEditorPlugin : IFieldPlugin
{
    public string DisplayName => "Text";
    public string? Icon => "📄";
    public string FieldType => "TextArea";

    public object? DefaultValue => string.Empty;
    
    public string[] SupportedExtensions => [".txt", ".json", ".xml", ".css", ".js"];
    
    public bool CanHandle(string fileExtension)
    {
        return SupportedExtensions.Contains(fileExtension, StringComparer.OrdinalIgnoreCase);
    }
    
    public RenderFragment RenderEditor(object? value, EventCallback<object?> onContentChanged)
    {
        return builder =>
        {
            builder.OpenElement(0, "textarea");
            builder.AddAttribute(1, "class", "editor-textarea text-editor");
            builder.AddAttribute(2, "placeholder", "Enter text content...");
            builder.AddAttribute(3, "value", value?.ToString() ?? string.Empty);
            builder.AddAttribute(4, "oninput", EventCallback.Factory.Create<Microsoft.AspNetCore.Components.ChangeEventArgs>(
                this, args => onContentChanged.InvokeAsync(args.Value?.ToString() ?? string.Empty)));
            builder.CloseElement();
        };
    }
}