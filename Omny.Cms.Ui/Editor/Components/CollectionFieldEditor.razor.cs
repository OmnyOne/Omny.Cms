using Microsoft.AspNetCore.Components;
using Omny.Cms.Editor;

namespace Omny.Cms.UiEditor.Components;

public class CollectionFieldEditorBase : ComponentBase
{
    [Inject] protected IEditorService EditorService { get; set; } = default!;
    [Inject] protected IPluginRegistry PluginRegistry { get; set; } = default!;

    [Parameter] public string FieldType { get; set; } = string.Empty;
    [Parameter] public CollectionFieldContent? Value { get; set; }
    [Parameter] public EventCallback<CollectionFieldContent> ValueChanged { get; set; }

    protected string[] _allowed = Array.Empty<string>();
    protected List<FieldContent> _items = new();
    protected Dictionary<int, string> _kinds = new();

    private string _lastFieldType = string.Empty;
    private CollectionFieldContent? _lastValue;
    private bool _shouldRender = true;

    protected override async Task OnParametersSetAsync()
    {
        bool parametersChanged = false;

        // Check if FieldType changed
        if (_lastFieldType != FieldType)
        {
            var def = await EditorService.GetFieldTypeDefinitionAsync(FieldType);
            _allowed = def?.AllowedFieldTypes ?? Array.Empty<string>();
            _lastFieldType = FieldType;
            parametersChanged = true;
        }

        // Check if Value changed by comparing the items
        if (HasValueChanged())
        {
            _items = Value?.Items.ToList() ?? new List<FieldContent>();
            await UpdateKindsForNewItems();
            _lastValue = Value;
            parametersChanged = true;
        }

        _shouldRender = parametersChanged;
    }

    private bool HasValueChanged()
    {
        if (_lastValue == null && Value == null)
            return false;
        
        if (_lastValue == null || Value == null)
            return true;

        var lastItems = _lastValue.Items;
        var currentItems = Value.Items;

        if (lastItems.Count != currentItems.Count)
            return true;

        for (int i = 0; i < lastItems.Count; i++)
        {
            if (lastItems[i].FieldType != currentItems[i].FieldType ||
                lastItems[i].Content != currentItems[i].Content)
            {
                return true;
            }
        }

        return false;
    }

    private async Task UpdateKindsForNewItems()
    {
        // Only update kinds for items that don't have them yet or have changed
        for (int i = 0; i < _items.Count; i++)
        {
            if (!_kinds.ContainsKey(i) || 
                (_lastValue != null && i < _lastValue.Items.Count && _lastValue.Items[i].FieldType != _items[i].FieldType))
            {
                _kinds[i] = await EditorService.GetFieldKindAsync(_items[i].FieldType);
            }
        }

        // Remove kinds for items that no longer exist
        var keysToRemove = _kinds.Keys.Where(k => k >= _items.Count).ToList();
        foreach (var key in keysToRemove)
        {
            _kinds.Remove(key);
        }
    }

    protected override bool ShouldRender()
    {
        return _shouldRender;
    }

    protected RenderFragment RenderItemEditor(int index) => builder =>
    {
        var item = _items[index];
        if (index > _kinds.Count - 1)
        {
            return;
        }
        var kind = _kinds[index];
        if (kind == "compound")
        {
            builder.OpenComponent<CompoundFieldEditor>(0);
            builder.AddAttribute(1, "FieldType", item.FieldType);
            builder.AddAttribute(2, "Value", item.Content);
            builder.AddAttribute(3, "ValueChanged", EventCallback.Factory.Create<string>(this, async val => { _items[index] = item with { Content = val }; await NotifyChanged(); }));
            builder.CloseComponent();
        }
        else if (kind == "collection")
        {
            var coll = System.Text.Json.JsonSerializer.Deserialize<CollectionFieldContent>(item.Content) ?? new CollectionFieldContent(new List<FieldContent>());
            builder.OpenComponent<CollectionFieldEditor>(0);
            builder.AddAttribute(1, "FieldType", item.FieldType);
            builder.AddAttribute(2, "Value", coll);
            builder.AddAttribute(3, "ValueChanged", EventCallback.Factory.Create<CollectionFieldContent>(this, async val => { _items[index] = item with { Content = System.Text.Json.JsonSerializer.Serialize(val) }; await NotifyChanged(); }));
            builder.CloseComponent();
        }
        else
        {
            var plugin = PluginRegistry.GetFieldPlugin(item.FieldType);
            builder.AddContent(0, plugin.RenderEditor(item.Content, EventCallback.Factory.Create<object?>(this, async v => { _items[index] = item with { Content = v?.ToString() ?? string.Empty }; await NotifyChanged(); })));
        }
    };

    protected async Task Add(string type)
    {
        var plugin = PluginRegistry.GetFieldPlugin(type);
        var defaultValue = plugin.DefaultValue?.ToString() ?? string.Empty;
        _items.Add(new FieldContent(type, defaultValue));
        _kinds[_items.Count - 1] = await EditorService.GetFieldKindAsync(type);
        _shouldRender = true;
        await NotifyChanged();
    }

    protected async Task Remove(int index)
    {
        if (index >= 0 && index < _items.Count)
        {
            _items.RemoveAt(index);
            
            // Rebuild kinds dictionary to maintain correct indices
            var newKinds = new Dictionary<int, string>();
            for (int i = 0; i < _items.Count; i++)
            {
                if (i < index && _kinds.ContainsKey(i))
                {
                    newKinds[i] = _kinds[i];
                }
                else if (i >= index && _kinds.ContainsKey(i + 1))
                {
                    newKinds[i] = _kinds[i + 1];
                }
            }
            _kinds = newKinds;
            
            _shouldRender = true;
            await NotifyChanged();
        }
    }

    protected async Task NotifyChanged()
    {
        await ValueChanged.InvokeAsync(new CollectionFieldContent(_items));
    }
}
