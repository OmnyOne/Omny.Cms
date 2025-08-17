using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Omny.Cms.Editor;

namespace Omny.Cms.UiEditor.Components;

public class MenuItemEditorBase : ComponentBase
{
    [Inject] protected IEditorService EditorService { get; set; } = default!;

    [Parameter] public string? Value { get; set; }
    [Parameter] public EventCallback<string> ValueChanged { get; set; }

    protected bool loading = true;
    protected bool _external;
    protected string? _name;
    protected string? _link;
    protected string? _target;

    protected List<(string Value, string Display)>? _options;

    protected string? ConfigType;
    protected string? ConfigNameField;
    protected string? ConfigLinkField;

    protected bool HasConfig => !string.IsNullOrEmpty(ConfigType);

    protected override async Task OnInitializedAsync()
    {
        ConfigType = (await EditorService.GetCustomDataAsync("MenuItemType"))?.ToString();
        ConfigNameField = (await EditorService.GetCustomDataAsync("MenuItemNameField"))?.ToString();
        ConfigLinkField = (await EditorService.GetCustomDataAsync("MenuItemLinkField"))?.ToString();

        if (ConfigType != null)
        {
            var items = await EditorService.GetContentItemsAsync(ConfigType);
            _options = items.Select(i =>
            {
                string display = i.Name;
                if (ConfigNameField != null && i.FieldValues != null && i.FieldValues.TryGetValue(ConfigNameField, out var d) && d is string ds)
                {
                    display = ds;
                }
                string value = i.Name;
                if (ConfigLinkField != null && i.FieldValues != null && i.FieldValues.TryGetValue(ConfigLinkField, out var l) && l is JsonElement el && el.ValueKind == JsonValueKind.String )
                {
                    value = el.ToString();
                }
                return (value, display);
            }).ToList();
        }

        ParseValue();
        loading = false;
    }

    protected override void OnParametersSet()
    {
        ParseValue();
    }

    private void ParseValue()
    {
        if (string.IsNullOrEmpty(Value))
        {
            _external = HasConfig;
            _name = string.Empty;
            _link = string.Empty;
            _target = _options?.FirstOrDefault().Value;
            return;
        }
        try
        {
            var data = JsonSerializer.Deserialize<MenuItemData>(Value!);
            if (data != null)
            {
                _external = data.External;
                _name = data.Name;
                _link = data.Link;
                _target = data.Target;
            }
        }
        catch
        {
            _external = true;
            _name = Value;
            _link = string.Empty;
        }
    }
    
    protected async Task NotifyChanged()
    {
        var data = new MenuItemData
        {
            External = _external,
            Name = _name,
            Link = _link,
            Target = _target
        };
        await ValueChanged.InvokeAsync(JsonSerializer.Serialize(data));
    }


    protected async Task OnExternalChanged(bool value)
    {
        _external = value;
        await NotifyChanged();
    }

    protected async Task OnNameChanged(string? value)
    {
        _name = value;
        await NotifyChanged();
    }

    protected async Task OnLinkChanged(string? value)
    {
        _link = value;
        await NotifyChanged();
    }

    protected async Task OnTargetChanged(string? value)
    {
        _target = value;
        await NotifyChanged();
    }

    private class MenuItemData
    {
        public bool External { get; set; }
        public string? Name { get; set; }
        public string? Link { get; set; }
        public string? Target { get; set; }
    }
}
