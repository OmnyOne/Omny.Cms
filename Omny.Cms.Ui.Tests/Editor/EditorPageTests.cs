using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Omny.Cms.UiRepositories.Files;
using Omny.Cms.UiEditor.Pages;
using Omny.Cms.Editor;
using MudBlazor.Services;
using Omny.Cms.UiRepositories.Services;
using NUnit.Framework;
using System.Collections.Generic;
using Omny.Cms.Manifest;
using Omny.Cms.UiRepositories.Files.GitHub;

namespace Omny.Cms.Ui.Tests.Editor;

public class EditorPageTests
{
    [Test]
    public void RendersContentTypesAndAllowsAddingItem()
    {
        using var ctx = new Bunit.TestContext();
        ctx.Services.AddMudServices();
        ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        var js = ctx.JSInterop;
        js.Setup<string?>("prompt", _ => true).SetResult("newitem");
        js.Setup<bool>("confirm", _ => true).SetResult(true);

        var serviceMock = new Mock<IEditorService>();
        serviceMock.Setup(s => s.GetContentTypesAsync())
            .ReturnsAsync(new[] { new ContentType("Page") });
        serviceMock.Setup(s => s.GetContentItemsAsync("Page"))
            .ReturnsAsync(new[] { new ContentItem("Page", "existing", new string[0]) });
        serviceMock.Setup(s => s.GetFolderFieldAsync("Page"))
            .ReturnsAsync((string?)null);
        serviceMock.Setup(s => s.UseRandomImageNamesAsync()).ReturnsAsync(false);
        serviceMock.Setup(s => s.CreateContentItemAsync("Page", "newitem"))
            .ReturnsAsync(new ContentItem("Page", "newitem", new string[0]) { FieldValues = new(), IsNew = true });
        serviceMock.Setup(s => s.GetFieldDefinitionsAsync("Page"))
            .ReturnsAsync(new[] { new FieldDefinition("Title", "text", null, "Title") });
        serviceMock.Setup(s => s.GetFieldKindAsync("text"))
            .ReturnsAsync("text");
        serviceMock.Setup(s => s.GetCustomDataAsync(It.IsAny<string>())).ReturnsAsync((object?)null);

        ctx.Services.AddSingleton(serviceMock.Object);
        ctx.Services.AddSingleton(Mock.Of<IRemoteFileService>());
        ctx.Services.AddSingleton(Mock.Of<IPluginRegistry>());
        ctx.Services.AddSingleton(Mock.Of<IAdvancedUserCheck>());
        var repoMgr = Mock.Of<IRepositoryManagerService>();
        ctx.Services.AddSingleton(repoMgr);
        ctx.Services.AddSingleton(new BuildWatcherService(Mock.Of<IGitHubClientProvider>(), repoMgr));

        var cut = ctx.RenderComponent<Omny.Cms.UiEditor.Pages.Editor>();

        cut.WaitForAssertion(() => StringAssert.Contains("existing", cut.Markup));

    }
}
