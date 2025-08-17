using Microsoft.AspNetCore.Components;

namespace Omny.Cms.UiEditor.Components;

public class TinyMceEditorBase : ComponentBase
{
    [Parameter]
    public string? Html { get; set; }

    [Parameter]
    public EventCallback<string> OnChanged { get; set; }

    protected string? _value;

    protected override void OnParametersSet()
    {
        _value = Html;
    }

    protected async Task OnValueChanged(string value)
    {
        Html = value;
        _value = value;
        await OnChanged.InvokeAsync(value);
    }
}
