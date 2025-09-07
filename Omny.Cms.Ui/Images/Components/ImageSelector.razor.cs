using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Forms;
using MudBlazor;
using Microsoft.JSInterop;
using Omny.Cms.Editor;
using Omny.Cms.UiImages.Services;
using Omny.Cms.UiRepositories.Services;
using System.IO;
using Omny.Cms.Ui;
using Omny.Cms.Ui.Images.Components;

namespace Omny.Cms.UiImages.Components;

public class ImageSelectorBase : ComponentBase
{
    [CascadingParameter] IMudDialogInstance MudDialog { get; set; } = default!;

    [Inject] protected IEditorService EditorService { get; set; } = default!;
    [Inject] protected IJSRuntime JS { get; set; } = default!;
    [Inject] protected IDialogService DialogService { get; set; } = default!;
    [Inject] protected BuildWatcherService BuildWatcher { get; set; } = default!;

    protected List<string>? _images;
    protected string? _selected;
    protected bool Uploading = false;
    [Parameter] public bool ShouldSelectImage { get; set; } = true;

    protected override async Task OnInitializedAsync()
    {
        _images = (await EditorService.GetImagesAsync()).ToList();
    }

    protected void Confirm()
    {
        if (!string.IsNullOrEmpty(_selected))
        {
            MudDialog.Close(DialogResult.Ok(_selected));
        }
    }

    protected async Task UploadFile(IBrowserFile? bf)
    {
        if (bf is null)
        {
            return;
        }
        var cropDialog = await DialogService.ShowAsync<ImageCropperDialog>(
            "Crop Image",
            new DialogParameters<ImageCropperDialog> { { x => x.File, bf } },
            new DialogOptions { FullScreen = true, CloseButton = true });
        var cropResult = await cropDialog.Result;
        if (cropResult.Canceled || cropResult.Data is not IEnumerable<byte> bytes)
        {
            return;
        }

        string name = bf.Name;
        bool random = await EditorService.UseRandomImageNamesAsync();
        if (!random)
        {
            var inputName = await JS.InvokeAsync<string?>("prompt", "Enter image name", Path.GetFileNameWithoutExtension(name));
            if (!string.IsNullOrWhiteSpace(inputName))
            {
                name = Path.ChangeExtension(inputName, UiConstants.ImageExtension);
            }
        }

        Uploading = true;
        StateHasChanged();
        await EditorService.UploadImageAsync(name, bytes.ToArray());
        await BuildWatcher.StartWatchingAsync(quiet: true);
        MudDialog.Close(DialogResult.Ok(name));
        Uploading = false;
        StateHasChanged();
    }

    protected void Cancel(MouseEventArgs _) => MudDialog.Cancel();
}
