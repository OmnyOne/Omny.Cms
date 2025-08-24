using Microsoft.AspNetCore.Components;
using Omny.Cms.Editor.Fields;
using System;

namespace Omny.Cms.Plugins.Fields;

public class DateTimeFieldPlugin : IFieldPlugin
{
    public string DisplayName => "Date/Time";
    public string? Icon => "📅";
    public string FieldType => "datetime";
    public object? DefaultValue => string.Empty;
    public string[] SupportedExtensions => Array.Empty<string>();
    public bool CanHandle(string fileExtension) => false;
    public RenderFragment RenderEditor(object? value, EventCallback<object?> onChanged)
    {
        return builder =>
        {
            builder.OpenElement(0, "input");
            builder.AddAttribute(1, "type", "datetime-local");
            string formatted = value switch
            {
                DateTime dt => dt.ToString("yyyy-MM-ddTHH:mm:ss"),
                DateTimeOffset dto => dto.ToString("yyyy-MM-ddTHH:mm:ss"),
                string s when DateTime.TryParse(s, out var parsed) => parsed.ToString("yyyy-MM-ddTHH:mm:ss"),
                _ => value?.ToString() ?? string.Empty
            };
            builder.AddAttribute(2, "value", formatted);
            builder.AddAttribute(3, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(this, e => onChanged.InvokeAsync(e.Value)));
            builder.AddAttribute(4, "class", "datetime-field");
            builder.CloseElement();
        };
    }
}
