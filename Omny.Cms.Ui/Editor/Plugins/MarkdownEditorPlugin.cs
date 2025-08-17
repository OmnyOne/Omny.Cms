using Microsoft.AspNetCore.Components;
using Omny.Cms.Editor.Fields;
using Omny.Cms.Ui.Components;

namespace Omny.Cms.Editor.Plugins;

public class MarkdownEditorPlugin : IFieldPlugin
{
    public string DisplayName => "Markdown";
    public string? Icon => "✏️";
    public string FieldType => "Markdown";

    public object? DefaultValue => string.Empty;

    public string[] SupportedExtensions => [".md", ".markdown"];

    public bool CanHandle(string fileExtension)
    {
        return SupportedExtensions.Contains(fileExtension, StringComparer.OrdinalIgnoreCase);
    }

    public RenderFragment RenderEditor(object? value, EventCallback<object?> onContentChanged)
    {
        return builder =>
        {
            builder.OpenComponent<JsFieldEditor>(0);
            builder.AddAttribute(1, "PluginName", "toast-markdown");
            builder.AddAttribute(2, "Value", value);
            builder.AddAttribute(3, "OnChanged", onContentChanged);
            builder.CloseComponent();
        };
    }
}
