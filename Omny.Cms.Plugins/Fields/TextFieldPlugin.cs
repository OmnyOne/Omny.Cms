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
            builder.OpenComponent(0, typeof(MudBlazor.MudTextField<string>));
            builder.AddAttribute(1, "Value", value?.ToString() ?? string.Empty);
            builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<string>(this,
                v => onChanged.InvokeAsync(v)));
            builder.AddAttribute(3, "Class", "text-field");
            builder.AddAttribute(4, "Placeholder", "Enter text...");
            builder.CloseComponent();
        };
    }
}
