using Microsoft.AspNetCore.Components;
using Blazored.TextEditor;

namespace Omny.Cms.UiEditor.Components;

public class OmnyTextEditorBase : ComponentBase, IDisposable
{
    [Parameter]
    public string? Html { get; set; }

    [Parameter]
    public EventCallback<string> OnChanged { get; set; }

    protected BlazoredTextEditor QuillHtmlSummary = default!;
    protected System.Threading.Timer? _timer;
    protected string? _lastHtml;
    protected int _intervalMs = 1000;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _timer = new System.Threading.Timer(async _ =>
            {
                try
                {
                    if (QuillHtmlSummary != null)
                    {
                        var currentHtml = await QuillHtmlSummary.GetHTML();
                        if (_lastHtml != currentHtml)
                        {
                            if (currentHtml == "<p>Loading</p>")
                            {
                                await InvokeAsync(async () => await QuillHtmlSummary.LoadHTMLContent(Html));
                                _lastHtml = Html;
                                return;
                            }

                            _lastHtml = currentHtml;
                            await InvokeAsync(() => OnChanged.InvokeAsync(currentHtml));
                        }
                    }
                }
                catch
                {
                }
            }, null, _intervalMs, _intervalMs);
        }
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }

    protected void InsertImageSummaryClick()
    {
    }
}
