using Microsoft.AspNetCore.Components;
using Omny.Cms.Editor;

namespace Omny.Cms.UiImages.Components;

public class ImagePreviewBase : ComponentBase
{
    [Inject] protected IEditorService EditorService { get; set; } = default!;

    [Parameter] public string FileName { get; set; } = string.Empty;

    protected string? _url;

    protected override async Task OnParametersSetAsync()
    {
        if (!string.IsNullOrEmpty(FileName))
        {
            _url = await EditorService.GetImageUrlAsync(FileName);
        }
    }
}
