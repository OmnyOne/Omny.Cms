using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Omny.Cms.Ui.Components;

public class JsFieldEditorBase : ComponentBase, IAsyncDisposable
{
    [Parameter]
    public string PluginName { get; set; } = string.Empty;

    [Parameter]
    public object? Value { get; set; }

    [Parameter]
    public EventCallback<object?> OnChanged { get; set; }

    [Inject]
    protected IJSRuntime JS { get; set; } = default!;

    protected string ElementId { get; } = $"js-field-{Guid.NewGuid()}";
    private DotNetObjectReference<JsFieldEditorBase>? _selfRef;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _selfRef = DotNetObjectReference.Create(this);
            await JS.InvokeVoidAsync("fieldPlugins.init", PluginName, ElementId, Value, _selfRef);
        }
    }

    [JSInvokable]
    public async Task OnValueChanged(object? value)
    {
        Value = value;
        await OnChanged.InvokeAsync(value);
    }

    public async ValueTask DisposeAsync()
    {
        if (_selfRef != null)
        {
            await JS.InvokeVoidAsync("fieldPlugins.destroy", PluginName, ElementId);
            _selfRef.Dispose();
        }
    }
}
