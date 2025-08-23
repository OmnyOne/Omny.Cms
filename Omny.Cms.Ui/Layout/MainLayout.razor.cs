using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Octokit.Internal;
using Omny.Cms.Ui.Authentication;

namespace Omny.Cms.UiLayout;

public class MainLayoutBase : LayoutComponentBase
{
    [Inject] private IJSRuntime? JS { get; set; }
    [Inject] private NavigationManager? NavigationManager { get; set; }
    [Inject] private IHttpClientFactory? HttpClientFactory { get; set; }
    [Inject] private CsrfTokenProvider? CsrfTokenProvider { get; set; }

    protected bool Loaded = false;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();
#if FREE_VERSION
        Loaded = true;
#else
        var httpClient = HttpClientFactory!.CreateClient("ApiClient");
        var response = await httpClient.PostAsync("csrf/token", null);
        response.EnsureSuccessStatusCode();
        var csrfResponse = await response.Content.ReadFromJsonAsync<CsrfResponse>();
        if (string.IsNullOrWhiteSpace(csrfResponse?.Token))
        {
            NavigationManager!.NavigateTo("/Account/Login", forceLoad: true);
            return;
        }

        CsrfTokenProvider!.CsrfToken = csrfResponse.Token;
        Loaded = true;
#endif
    }

    protected async Task Logout()
    {
        if (JS != null)
        {
            var module = await JS.InvokeAsync<IJSObjectReference>("import", "../Layout/MainLayout.razor.js");
            await module.InvokeVoidAsync("omnyLogout");
        }
#if FREE_VERSION
        NavigationManager!.NavigateTo("/", forceLoad: true);
#else
        NavigationManager!.NavigateTo("/Account/Logout", forceLoad: true);
#endif
    }
}

public record CsrfResponse(string Token);

