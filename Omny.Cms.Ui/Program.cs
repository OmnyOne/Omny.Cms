using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Octokit;
using Omny.Cms.Ui;
using Omny.Cms.UiRepositories.Files;
using Omny.Cms.UiRepositories.Models;
using Omny.Cms.Editor;
using Omny.Cms.Editor.ContentTypes;
using Omny.Cms.Plugins.Page;
using Omny.Cms.Plugins.Menu;
using Omny.Cms.Plugins.Hexo;
using Omny.Cms.Plugins.Fields;
using Omny.Cms.Plugins.Infrastructure;
using Omny.Cms.UiRepositories.Services;
using Omny.Cms.UiImages.Services;
using MudBlazor.Services;
using Cropper.Blazor.Extensions;
using Omny.Cms.Editor.Fields;
using Omny.Cms.Editor.Plugins;
using Omny.Cms.Ui.Authentication;
using Omny.Cms.UiRepositories.Files.GitHub;
using Omny.Cms.Abstractions.Manifest;
using Omny.Cms.Core.Editor;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiRoot = "api/";

builder.Services.AddBlazoredLocalStorage();
builder.Services.AddMudServices();
builder.Services.AddCropper();
builder.Services.AddScoped<IRepositoryService, RepositoryService>();
builder.Services.AddScoped<ApiFileService>();
builder.Services.AddScoped<IRepositoryManagerService, RepositoryManagerService>();
builder.Services.AddScoped<IAdvancedUserCheck, RepositoryManagerService>();
builder.Services.AddScoped<IGitHubClientProvider, GitHubClientProvider>();
builder.Services.AddScoped<GitHubFileService>();
builder.Services.AddScoped<IRemoteFileService, RepositoryRemoteFileService>();
builder.Services.AddScoped<IImageStorageService, S3ImageStorageService>();
builder.Services.AddScoped<IContentTypePlugin,PagePlugin>();
builder.Services.AddScoped<IContentTypePlugin, MenuPlugin>();
builder.Services.AddScoped<IContentTypePlugin, HexoPostPlugin>();
builder.Services.AddScoped<IContentTypeSerializer, ContentTypeSerializer>();
builder.Services.AddScoped<DefaultContentTypeSerializer>();
builder.Services.AddScoped<IContentTypeSerializerPlugin, DefaultContentTypeSerializer>();
builder.Services.AddScoped<IContentTypeSerializerPlugin, HexoPostSerializer>();

builder.Services.AddScoped<IFieldPlugin, TinyMceHtmlEditorPlugin>();
builder.Services.AddScoped<IFieldPlugin, Omny.Cms.Editor.Plugins.MarkdownEditorPlugin>();
builder.Services.AddScoped<IFieldPlugin, QuillHtmlEditorPlugin>();
builder.Services.AddScoped<IFieldPlugin, TextEditorPlugin>();
builder.Services.AddScoped<IFieldPlugin, TextFieldPlugin>();
builder.Services.AddScoped<IFieldPlugin, ImageFieldPlugin>();
builder.Services.AddScoped<IFieldPlugin, ImageTextFieldPlugin>();
builder.Services.AddScoped<IFieldPlugin, MenuItemFieldPlugin>();
builder.Services.AddScoped<IFieldPlugin, DateFieldPlugin>();

builder.Services.AddScoped<IEditorService, ManifestEditorService>();
builder.Services.AddSingleton<IManifestProvider, ManifestProvider>();
builder.Services.AddScoped<IPluginRegistry, PluginRegistry>();
builder.Services.AddScoped<BuildWatcherService>();

// Register editor plugins
builder.Services.AddScoped<Omny.Cms.Editor.Plugins.MarkdownEditorPlugin>();
builder.Services.AddScoped<Omny.Cms.Editor.Plugins.QuillHtmlEditorPlugin>();
builder.Services.AddScoped<Omny.Cms.Editor.Plugins.TinyMceHtmlEditorPlugin>();
builder.Services.AddScoped<Omny.Cms.Plugins.Fields.TextEditorPlugin>();
builder.Services.AddSingleton<AuthRedirectHandler>();
#if !FREE_VERSION
builder.Services.AddSingleton<CsrfTokenProvider>();
builder.Services.AddApiAuthorization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpClient("ApiClient",
        client => client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress + apiRoot))
    .AddHttpMessageHandler<AuthRedirectHandler>();
#else
builder.Services.AddHttpClient("ApiClient",
        client => client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress + apiRoot));
#endif
builder.Services.AddTransient(sp =>
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("ApiClient"));

await builder.Build().RunAsync();