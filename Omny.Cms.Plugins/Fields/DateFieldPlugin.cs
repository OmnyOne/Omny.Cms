using Microsoft.AspNetCore.Components;
using Omny.Cms.Editor.Fields;

namespace Omny.Cms.Plugins.Fields;

public class DateFieldPlugin : IFieldPlugin
{
    public string DisplayName => "Date";
    public string? Icon => "📅";
    public string FieldType => "date";
    public object? DefaultValue => string.Empty;
    public string[] SupportedExtensions => Array.Empty<string>();
    public bool CanHandle(string fileExtension) => false;
    public RenderFragment RenderEditor(object? value, EventCallback<object?> onChanged)
    {
        return builder =>
        {
            builder.OpenElement(0, "input");
            builder.AddAttribute(1, "type", "date");
            builder.AddAttribute(2, "value", value?.ToString() ?? string.Empty);
            builder.AddAttribute(3, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(this, e => onChanged.InvokeAsync(e.Value)));
            builder.AddAttribute(4, "class", "date-field");
            builder.CloseElement();
        };
    }
}
