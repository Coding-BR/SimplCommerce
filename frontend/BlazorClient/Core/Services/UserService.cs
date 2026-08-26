using System.Net.Http.Json;
using BlazorClient.Models;

using BlazorClient.Core.Interfaces;

namespace BlazorClient.Core.Services;

public class UserService : IUserService
{
    private readonly IHttpClientFactory _factory;

    // Client-side cache for user profile
    private UserProfile? _cachedProfile;
    private DateTime _cacheExpiry = DateTime.MinValue;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(3);

    public UserService(IHttpClientFactory factory)
    {
        _factory = factory;
    }

    private HttpClient AuthClient => _factory.CreateClient("AuthenticatedAPI");

    public async Task<UserListResponse> GetAllUsers(int page = 1, int pageSize = 20, string? search = null, string? pageToken = null)
    {
        var queryParams = new List<string>
        {
            $"page={page}",
            $"pageSize={pageSize}"
        };
        
        if (!string.IsNullOrWhiteSpace(search))
        {
            queryParams.Add($"search={Uri.EscapeDataString(search)}");
        }

        if (!string.IsNullOrWhiteSpace(pageToken))
        {
            queryParams.Add($"pageToken={Uri.EscapeDataString(pageToken)}");
        }
        
        var url = $"api/admin/users?{string.Join("&", queryParams)}";
        return await AuthClient.GetFromJsonAsync<UserListResponse>(url) ?? new UserListResponse();
    }

    public async Task PromoteToAdmin(string uid)
    {
        var response = await AuthClient.PostAsync($"api/admin/users/{uid}/promote", null);
        response.EnsureSuccessStatusCode();
    }

    public async Task DemoteFromAdmin(string uid)
    {
        var response = await AuthClient.PostAsync($"api/admin/users/{uid}/demote", null);
        response.EnsureSuccessStatusCode();
    }

    public async Task<UserProfile?> GetProfileAsync()
    {
        // OPTIMIZATION: Return cached profile if still valid
        if (_cachedProfile != null && DateTime.UtcNow < _cacheExpiry)
        {
            return _cachedProfile;
        }

        try
        {
            var profile = await AuthClient.GetFromJsonAsync<UserProfile>("api/users/profile");
            if (profile != null)
            {
                _cachedProfile = profile;
                _cacheExpiry = DateTime.UtcNow.Add(CacheDuration);
            }
            return profile;
        }
        catch
        {
            return null;
        }
    }

    public async Task<UserProfile?> UpdateProfileAsync(UserProfile profile)
    {
        var response = await AuthClient.PutAsJsonAsync("api/users/profile", profile);
        if (response.IsSuccessStatusCode)
        {
            var updated = await response.Content.ReadFromJsonAsync<UserProfile>();
            // Update local cache immediately after successful update
            _cachedProfile = updated;
            _cacheExpiry = DateTime.UtcNow.Add(CacheDuration);
            return updated;
        }
        return null;
    }

    public async Task<AccountDeletionPreview?> GetAccountDeletionPreviewAsync()
    {
        try { return await AuthClient.GetFromJsonAsync<AccountDeletionPreview>("api/users/privacy/deletion-preview"); }
        catch { return null; }
    }

    public async Task<AccountDeletionResult> RequestAccountDeletionAsync(string currentPassword)
    {
        try
        {
            var response = await AuthClient.PostAsJsonAsync("api/users/privacy/delete-account", new { currentPassword });
            var result = await response.Content.ReadFromJsonAsync<AccountDeletionResult>() ?? new AccountDeletionResult();
            result.Success = response.IsSuccessStatusCode;
            if (!result.Success && string.IsNullOrWhiteSpace(result.Message)) result.Message = "Não foi possível concluir a solicitação. Confirme sua senha atual e tente novamente.";
            return result;
        }
        catch
        {
            return new AccountDeletionResult { Success = false, Message = "Não foi possível concluir a solicitação. Tente novamente." };
        }
    }

    public void InvalidateProfileCache()
    {
        _cachedProfile = null;
        _cacheExpiry = DateTime.MinValue;
    }
}





