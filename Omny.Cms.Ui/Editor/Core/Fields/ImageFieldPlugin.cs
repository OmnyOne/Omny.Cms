using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Omny.Cms.UiImages.Components;
using Omny.Cms.Editor.Fields;
using MudBlazor;

namespace Omny.Cms.Editor.Fields;

public class ImageFieldPlugin : IFieldPlugin
{
    private readonly IDialogService _dialogs;

    public ImageFieldPlugin(IDialogService dialogs)
    {
        _dialogs = dialogs;
    }

    bool IFieldPlugin.ShouldNotFetchContent => true;
    public string DisplayName => "Image";
    public string? Icon => "🖼️";
    public string FieldType => "image";

    public object? DefaultValue => string.Empty;

    public string[] SupportedExtensions => Array.Empty<string>();

    public bool CanHandle(string fileExtension) => false;

    public RenderFragment RenderEditor(object? value, EventCallback<object?> onChanged)
    {
        return builder =>
        {
            builder.OpenElement(0, "div");
            var current = value?.ToString();
            if (!string.IsNullOrEmpty(current))
            {
                builder.OpenComponent<ImagePreview>(1);
                builder.AddAttribute(2, "FileName", current);
                builder.CloseComponent();
            }
            builder.OpenComponent<MudButton>(3);
            builder.AddAttribute(4, "OnClick", EventCallback.Factory.Create<MouseEventArgs>(this, (_) => OpenSelector(onChanged)));
            builder.AddAttribute(5, "Variant", Variant.Filled);
            builder.AddAttribute(6, "ChildContent", (RenderFragment)(builder2 => builder2.AddContent(0, "Select Image")));
            builder.CloseComponent();
            builder.CloseElement();
        };
    }

    private async Task OpenSelector(EventCallback<object?> onChanged)
    {
        var options = new DialogOptions { FullScreen = true, CloseButton = true };
        var dialog = await _dialogs.ShowAsync<ImageSelector>("Select Image", options);
        var result = await dialog.Result;
        if (!result.Canceled && result.Data is string val)
        {
            await onChanged.InvokeAsync(val);
        }
    }
}
