using System.Security.Claims;
using BlazorClient.Core.Interfaces;
using Microsoft.AspNetCore.Components.Authorization;

namespace BlazorClient.Core.Services;

public sealed class CustomAuthenticationStateProvider(IAuthService authService) : AuthenticationStateProvider
{
    private readonly ClaimsPrincipal anonymous = new(new ClaimsIdentity());

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var user = await authService.GetCurrentUser();
        if (user is null) return new AuthenticationState(anonymous);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Uid),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new(ClaimTypes.Name, user.DisplayName ?? user.Email ?? string.Empty)
        };
        if (user.IsAdmin)
        {
            // Keep both claim names: Blazor uses ClaimTypes.Role while JWT-aware
            // components commonly look for the compact "role" claim.
            claims.Add(new Claim(ClaimTypes.Role, "Admin"));
            claims.Add(new Claim("role", "Admin"));
        }
        var identity = new ClaimsIdentity(claims, "IdealCreative", ClaimTypes.Name, ClaimTypes.Role);
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    public void NotifyUserAuthentication(string token) => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());

    public void NotifyUserLogout() => NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(anonymous)));

    public Task RefreshAuthenticationState()
    {
        var state = GetAuthenticationStateAsync();
        NotifyAuthenticationStateChanged(state);
        return state;
    }
}
