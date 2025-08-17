using Microsoft.AspNetCore.Components;
using Blazored.TextEditor;
using Omny.Cms.UiEditor.Components;
using Omny.Cms.Editor.Fields;

namespace Omny.Cms.Editor.Plugins;

public class QuillHtmlEditorPlugin : IFieldPlugin
{
    public string DisplayName => "HTML";
    public string? Icon => "📄";
    public string FieldType => "HTML";

    public object? DefaultValue => string.Empty;
    
    public string[] SupportedExtensions => [".html", ".htm"];
    
    public bool CanHandle(string fileExtension)
    {
        return SupportedExtensions.Contains(fileExtension, StringComparer.OrdinalIgnoreCase);
    }
    
    public RenderFragment RenderEditor(object? value, EventCallback<object?> onContentChanged)
    {
        return builder =>
        {
            builder.OpenComponent<OmnyTextEditor>(0);
            builder.AddAttribute(1, "Html", value?.ToString());
            builder.AddAttribute(2, "OnChanged", EventCallback.Factory.Create<string>(this, v => onContentChanged.InvokeAsync(v)));
            builder.CloseComponent();
        };
    }
}
