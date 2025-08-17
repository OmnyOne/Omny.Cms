using Microsoft.AspNetCore.Components;
using Omny.Cms.Editor.Fields;
using Omny.Cms.UiEditor.Components;

namespace Omny.Cms.Editor.Fields;

public class MenuItemFieldPlugin : IFieldPlugin
{
    public string DisplayName => "Menu Item";
    public string? Icon => "📄";
    public string FieldType => "MenuItem";

    public object? DefaultValue => "{}";

    public string[] SupportedExtensions => Array.Empty<string>();

    public bool CanHandle(string fileExtension) => false;

    public RenderFragment RenderEditor(object? value, EventCallback<object?> onChanged)
    {
        return builder =>
        {
            builder.OpenComponent<MenuItemEditor>(0);
            builder.AddAttribute(1, "Value", value?.ToString());
            builder.AddAttribute(2, "ValueChanged", EventCallback.Factory.Create<string>(this, v => onChanged.InvokeAsync(v)));
            builder.CloseComponent();
        };
    }
}
