using Microsoft.AspNetCore.Components;
using Omny.Cms.Editor;
using Omny.Cms.Manifest;

namespace Omny.Cms.UiEditor.Components;

public class CompoundFieldEditorBase : ComponentBase
{
    [Inject] protected IEditorService EditorService { get; set; } = default!;
    [Inject] protected IPluginRegistry PluginRegistry { get; set; } = default!;

    [Parameter] public string FieldType { get; set; } = string.Empty;
    [Parameter] public string? Value { get; set; }
    [Parameter] public EventCallback<string> ValueChanged { get; set; }

    protected SubFieldDefinition[]? _subFields;
    protected Dictionary<string, string?> _values = new();
    protected Dictionary<string, string> _kinds = new();

    protected override async Task OnParametersSetAsync()
    {
        var def = await EditorService.GetFieldTypeDefinitionAsync(FieldType);
        _subFields = def?.SubFields ?? Array.Empty<SubFieldDefinition>();
        if (Value != null)
        {
            try
            {
                _values = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string?>>(Value!) ?? new();
            }
            catch
            {
                _values = new();
            }
        }
        foreach (var sub in _subFields)
        {
            var key = sub.FieldType;
            if (!_values.ContainsKey(key)) _values[key] = null;
            _kinds[key] = await EditorService.GetFieldKindAsync(key);
        }
    }

    protected RenderFragment RenderSubField(string key) => builder =>
    {
        var kind = _kinds.TryGetValue(key, out var k) ? k : key;
        if (kind == "compound")
        {
            builder.OpenComponent<CompoundFieldEditor>(0);
            builder.AddAttribute(1, "FieldType", key);
            builder.AddAttribute(2, "Value", _values[key]);
            builder.AddAttribute(3, "ValueChanged", EventCallback.Factory.Create<string>(this, (string val) => OnSubChanged(key, val)));
            builder.CloseComponent();
        }
        else if (kind == "collection")
        {
            var coll = _values[key] is string s && !string.IsNullOrEmpty(s)
                ? System.Text.Json.JsonSerializer.Deserialize<CollectionFieldContent>(s) ?? new CollectionFieldContent(new List<FieldContent>())
                : new CollectionFieldContent(new List<FieldContent>());
            builder.OpenComponent<CollectionFieldEditor>(0);
            builder.AddAttribute(1, "FieldType", key);
            builder.AddAttribute(2, "Value", coll);
            builder.AddAttribute(3, "ValueChanged", EventCallback.Factory.Create<CollectionFieldContent>(this, async c => await OnSubChanged(key, System.Text.Json.JsonSerializer.Serialize(c))));
            builder.CloseComponent();
        }
        else
        {
            var plugin = PluginRegistry.GetFieldPlugin(key);
            builder.AddContent(0, plugin.RenderEditor(_values[key], EventCallback.Factory.Create<object?>(this, val => OnSubChanged(key, val?.ToString()))));
        }
    };

    protected async Task OnSubChanged(string key, string? newVal)
    {
        _values[key] = newVal;
        var json = System.Text.Json.JsonSerializer.Serialize(_values);
        await ValueChanged.InvokeAsync(json);
    }
}
