using System.Net.Http.Headers;
using System.Net.Http.Json;
using BlazorClient.Core.Interfaces;
using BlazorClient.Models;
using Microsoft.JSInterop;

namespace BlazorClient.Core.Services;

public sealed class AuthService(IHttpClientFactory clients, IJSRuntime js) : IAuthService
{
    private const string TokenKey = "idealcreative.access_token";

    public async Task<AuthResult> SignInWithEmail(string email, string password)
    {
        try
        {
            var response = await clients.CreateClient("PublicAPI").PostAsJsonAsync("api/auth/login", new { email, password });
            if (!response.IsSuccessStatusCode)
                return new AuthResult { Success = false, Error = "E-mail ou senha inválidos." };
            return await SaveResponse(response);
        }
        catch (Exception exception)
        {
            return new AuthResult { Success = false, Error = exception.Message };
        }
    }

    public async Task<AuthResult> RegisterWithEmail(string email, string password, string displayName)
    {
        try
        {
            var response = await clients.CreateClient("PublicAPI").PostAsJsonAsync("api/auth/register", new { email, password, displayName });
            if (!response.IsSuccessStatusCode)
                return new AuthResult { Success = false, Error = "Não foi possível criar a conta." };
            return await SaveResponse(response);
        }
        catch (Exception exception)
        {
            return new AuthResult { Success = false, Error = exception.Message };
        }
    }

    public async Task<AuthResult> RequestPasswordReset(string email)
    {
        try
        {
            var response = await clients.CreateClient("PublicAPI").PostAsJsonAsync("api/auth/forgot-password", new { email });
            var message = await ReadMessage(response, "Não foi possível solicitar a recuperação de senha.");
            return new AuthResult { Success = response.IsSuccessStatusCode, Error = response.IsSuccessStatusCode ? null : message, Message = message };
        }
        catch (Exception exception)
        {
            return new AuthResult { Success = false, Error = exception.Message };
        }
    }

    public async Task<AuthResult> ResetPassword(string email, string token, string password)
    {
        try
        {
            var response = await clients.CreateClient("PublicAPI").PostAsJsonAsync("api/auth/reset-password", new { email, token, password });
            var message = await ReadMessage(response, "Não foi possível redefinir a senha.");
            return new AuthResult { Success = response.IsSuccessStatusCode, Error = response.IsSuccessStatusCode ? null : message, Message = message };
        }
        catch (Exception exception)
        {
            return new AuthResult { Success = false, Error = exception.Message };
        }
    }

    public async Task SignOut() => await js.InvokeVoidAsync("localStorage.removeItem", TokenKey);

    public async Task<string?> GetCurrentUserToken()
    {
        try { return await js.InvokeAsync<string?>("localStorage.getItem", TokenKey); }
        catch { return null; }
    }

    public async Task<UserInfo?> GetCurrentUser()
    {
        var token = await GetCurrentUserToken();
        if (string.IsNullOrWhiteSpace(token)) return null;
        var request = new HttpRequestMessage(HttpMethod.Get, "api/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await clients.CreateClient("PublicAPI").SendAsync(request);
        if (!response.IsSuccessStatusCode) return null;
        var user = await response.Content.ReadFromJsonAsync<ApiUser>();
        return user is null ? null : new UserInfo { Uid = user.Id, Email = user.Email, DisplayName = user.DisplayName, EmailVerified = true, IsAdmin = user.IsAdmin };
    }

    private async Task<AuthResult> SaveResponse(HttpResponseMessage response)
    {
        var data = await response.Content.ReadFromJsonAsync<ApiAuthResponse>();
        if (data is null || string.IsNullOrWhiteSpace(data.AccessToken))
            return new AuthResult { Success = false, Error = "Resposta de autenticação inválida." };
        await js.InvokeVoidAsync("localStorage.setItem", TokenKey, data.AccessToken);
        return new AuthResult
        {
            Success = true,
            Token = data.AccessToken,
            User = new UserInfo { Uid = data.UserId, Email = data.Email, DisplayName = data.DisplayName, EmailVerified = true, IsAdmin = data.IsAdmin }
        };
    }

    private static async Task<string> ReadMessage(HttpResponseMessage response, string fallback)
    {
        try
        {
            var body = await response.Content.ReadFromJsonAsync<ApiMessage>();
            return string.IsNullOrWhiteSpace(body?.Message) ? fallback : body.Message;
        }
        catch { return fallback; }
    }

    private sealed record ApiAuthResponse(string AccessToken, string UserId, string Email, string DisplayName, bool IsAdmin);
    private sealed record ApiUser(string Id, string? Email, string? DisplayName, bool IsAdmin);
    private sealed record ApiMessage(string? Message);
}
