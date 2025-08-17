using Microsoft.AspNetCore.Components;
using Omny.Cms.Editor.Fields;

namespace Omny.Cms.Plugins.Fields;

public class TextFieldPlugin : IFieldPlugin
{
    public string DisplayName => "Text";
    public string? Icon => "📝";
    public string FieldType => "text";

    public object? DefaultValue => string.Empty;

    public string[] SupportedExtensions => Array.Empty<string>();

    public bool CanHandle(string fileExtension) => false;

    public RenderFragment RenderEditor(object? value, EventCallback<object?> onChanged)
    {
        return builder =>
        {
            builder.OpenElement(0, "input");
            builder.AddAttribute(1, "value", value?.ToString() ?? string.Empty);
            builder.AddAttribute(2, "oninput", EventCallback.Factory.Create<ChangeEventArgs>(this,
                e => onChanged.InvokeAsync(e.Value)));
            builder.AddAttribute(3,"class", "text-field");
            builder.AddAttribute(4,"placeholder", "Enter text...");
            builder.CloseElement();
        };
    }
}
