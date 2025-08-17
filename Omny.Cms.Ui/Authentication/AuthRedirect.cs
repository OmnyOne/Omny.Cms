using System.Net;
using Microsoft.AspNetCore.Components;

namespace Omny.Cms.Ui.Authentication;

public class AuthRedirectHandler(NavigationManager navigationManager, CsrfTokenProvider csrfTokenProvider) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (csrfTokenProvider.CsrfToken is not null)
        {
            // Add CSRF token to the request headers
            request.Headers.Add("X-CSRF-Token", csrfTokenProvider.CsrfToken);
        }
        
        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            // Redirect to the login page
            navigationManager.NavigateTo("/Account/Login", forceLoad: true);
        }

        return response;
    }
}

public class CsrfTokenProvider
{
    public string? CsrfToken { get; set; } = null;
}
