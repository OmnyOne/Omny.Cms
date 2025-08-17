using Microsoft.AspNetCore.Components;
using Omny.Cms.Editor.Fields;
using Omny.Cms.UiEditor.Components;

namespace Omny.Cms.Editor.Fields;

/// <summary>
/// Compound field combining an image and text value.
/// </summary>
public class ImageTextFieldPlugin : IFieldPlugin
{
    public string DisplayName => "Image + Text";
    public string? Icon => "🖼️📝";
    public string FieldType => "ImageText";
    public object? DefaultValue => System.Text.Json.JsonSerializer.Serialize(new { image = string.Empty, text = string.Empty });
    public string[] SupportedExtensions => Array.Empty<string>();
    public bool CanHandle(string fileExtension) => false;

    public RenderFragment RenderEditor(object? value, EventCallback<object?> onChanged)
    {
        return builder =>
        {
            builder.OpenComponent<CompoundFieldEditor>(0);
            builder.AddAttribute(1, "FieldType", FieldType);
            builder.AddAttribute(2, "Value", value?.ToString());
            builder.AddAttribute(3, "ValueChanged", EventCallback.Factory.Create<string>(this, v => onChanged.InvokeAsync(v)));
            builder.CloseComponent();
        };
    }
}
