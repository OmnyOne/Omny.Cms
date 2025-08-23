using Microsoft.Playwright;
using System.Net.Http.Json;
using System.Linq;
using NUnit.Framework;
using NUnit.Framework.Constraints;
using RepositoryInfo = Omny.Cms.UiRepositories.Models.RepositoryInfo;

namespace Omny.Cms.Ui.Tests;

[TestFixture]
public class LocalRepoPlaywrightTests
{
    private PlaywrightFixture? _playwrightFixture;
    private IPage? _currentPage;

    [OneTimeSetUp]
    public async Task OneTimeSetup()
    {
        _playwrightFixture = new PlaywrightFixture();
        await _playwrightFixture.StartAsync();
    }

    [OneTimeTearDown]
    public void OneTimeTeardown()
    {
        _playwrightFixture?.Dispose();
    }

    [TestCase("Local 1")]
    [TestCase("Local 2")]
    [TestCase("Local 3")]
    public async Task RepositoryEndpointUsesApiFlag(string repoName)
    {
        if (_playwrightFixture!.AspireFailed && (repoName == "Local 2" || repoName == "Local 3"))
        {
            Assert.Ignore("Aspire environment not available, skipping test.");
        }
        var repos = await _playwrightFixture.Client!.GetFromJsonAsync<List<RepositoryInfo>>("api/repositories");
        Assert.IsNotNull(repos);
        Assert.IsTrue(repos!.First(r => r.Name == repoName).UseApiFileService);

        var browserContext = await _playwrightFixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = _playwrightFixture.Factory!.BaseUri!.ToString()
        });
        var browserPage = await browserContext.NewPageAsync();
        await browserPage.GotoAsync("/");
        await browserPage.WaitForSelectorAsync("#app");
        await browserPage.SelectOptionAsync("#repository-dropdown", new[] { new SelectOptionValue { Label = repoName } });
        await browserPage.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    
    public async Task CanCreatePage()
    {
        var browserContext = await _playwrightFixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = _playwrightFixture.Factory!.BaseUri!.ToString()
        });
        var browserPage = await browserContext.NewPageAsync();
        _currentPage = browserPage;
        await browserPage.GotoAsync("/");
        await browserPage.WaitForSelectorAsync("#app");
        await browserPage.WaitForSelectorAsync("button:has-text('Add')");
        var addButton = browserPage.GetByRole(AriaRole.Button, new() { Name = "Add" });
        await addButton.WaitForAsync();
        
        browserPage.Dialog += async (_, dialog) =>
        {
            if (dialog.Type != "prompt")
            {
                await dialog.DismissAsync();
                return;
            }

            string message = dialog.Message.ToLowerInvariant();

            if (message.StartsWith("enter name"))
            {
                await dialog.AcceptAsync("Home");
            }
            else if (message.StartsWith("enter path"))
            {
                await dialog.AcceptAsync("index");
            }
            else
            {
                await dialog.DismissAsync();
            }
        };

        await addButton.ClickAsync(new LocatorClickOptions()
        {
            Force = true
        });
        // wait for save button to appear
        await browserPage.WaitForSelectorAsync("button:has-text('Save')");
        var pathInput = await browserPage.WaitForSelectorAsync(".text-field");
        // set the text of the input field
        Assert.AreEqual("index", await pathInput!.InputValueAsync());
        Assert.IsNotNull(pathInput);
        await pathInput!.FillAsync("edited");
        Assert.AreEqual("edited", await pathInput!.InputValueAsync());
        var saveButton = browserPage.GetByRole(AriaRole.Button, new() { Name = "Save" });
        bool isDisabled = await saveButton.IsDisabledAsync();
        while (isDisabled)
        {
            await Task.Delay(100);
            isDisabled = await saveButton.IsDisabledAsync();
        }
        await saveButton.ClickAsync();
        // wait for save button to be disabled
        isDisabled =await saveButton.IsDisabledAsync();
        while (!isDisabled)
        {
            await Task.Delay(100);
            isDisabled = await saveButton.IsDisabledAsync();
        }
        var closeButton = await browserPage.WaitForSelectorAsync("button:has-text('Close')");
        await closeButton!.ClickAsync();
        var editButton = await browserPage.WaitForSelectorAsync("button:has-text('Edit')");
        await editButton!.ClickAsync();
        await browserPage.WaitForSelectorAsync("button:has-text('Close')");
        var pathField = await browserPage.WaitForSelectorAsync(".text-field");
        Assert.AreEqual("edited", await pathField!.InputValueAsync());
    }
    
    [Test]
    [TestCase("Local 1")]
    [TestCase("Local 2")]
    [TestCase("Local 3")]
    public async Task CanUploadImageViaSelector(string repoName)
    {
        if (_playwrightFixture!.AspireFailed && (repoName == "Local 2" || repoName == "Local 3"))
        {
            Assert.Ignore("Aspire environment not available, skipping test.");
        }
        var browserContext = await _playwrightFixture!.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = _playwrightFixture.Factory!.BaseUri!.ToString()
        });
        var page = await browserContext.NewPageAsync();
        _currentPage = page;

        await page.GotoAsync("/");
        await page.SelectOptionAsync("#repository-dropdown", new[] { new SelectOptionValue { Label = repoName } });
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        
        var loadImageButton = await page.WaitForSelectorAsync("button:has-text('Load Image')");
        
        await loadImageButton!.ClickAsync();

        var loadPictureButton = page.GetByRole(AriaRole.Button, new() { Name = "Load picture" });
        await loadPictureButton.WaitForAsync();

        string logoPath = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory,
            "../../../..", "Omny.Cms.Ui", "wwwroot", "icon-192.png"));
        await page.SetInputFilesAsync("input[type=file]", logoPath);

        var saveButton = page.GetByRole(AriaRole.Button, new() { Name = "Save" });
        await saveButton.WaitForAsync();
        await saveButton.ClickAsync();
        await page.WaitForSelectorAsync("button:has-text('Save')", new PageWaitForSelectorOptions
        {
            State = WaitForSelectorState.Detached
        });
       
        loadImageButton = await page.WaitForSelectorAsync("button:has-text('Load Image')");
        
        await loadImageButton!.ClickAsync();
        loadPictureButton = page.GetByRole(AriaRole.Button, new() { Name = "Load picture" });
        await loadPictureButton.WaitForAsync();
        
        await page.WaitForSelectorAsync("button:has-text('Load picture')");
        

        var imagePreview = await page.WaitForSelectorAsync("img");
        Assert.IsNotNull(imagePreview);
        var imageSrc = await imagePreview!.GetAttributeAsync("src");
        Assert.IsNotNull(imageSrc);
        if (!imageSrc!.StartsWith("data"))
        {
            Assert.That(imageSrc, Does.EndWith("icon-192.png"));
        }
    }
    
    [TearDown]
    public async Task TearDown()
    {
        if (_currentPage is not null)
        {
            await _currentPage.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = Path.Combine("test-screenshots",$"{TestContext.CurrentContext.Test.Name}.png"),
                FullPage = true
            });
        }
    }
}
