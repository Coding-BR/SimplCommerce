using Microsoft.AspNetCore.Components.WebAssembly.Http;

using BlazorClient.Core.Interfaces;

namespace BlazorClient.Core.Handlers;

public class AuthenticationMessageHandler : DelegatingHandler
{
    private readonly IAuthService _authService;

    public AuthenticationMessageHandler(IAuthService authService)
    {
        _authService = authService;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Obter token do usuário atual
        var token = await _authService.GetCurrentUserToken();

        if (!string.IsNullOrEmpty(token))
        {
            // Adicionar header Authorization
            request.Headers.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }
        
        // Ensure cookies are handled for cross-origin requests
        request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);

        return await base.SendAsync(request, cancellationToken);
    }
}
