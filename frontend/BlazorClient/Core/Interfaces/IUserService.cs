using BlazorClient.Models;

namespace BlazorClient.Core.Interfaces;

public interface IUserService
{
    Task<UserListResponse> GetAllUsers(int page = 1, int pageSize = 20, string? search = null, string? pageToken = null);
    Task PromoteToAdmin(string uid);
    Task DemoteFromAdmin(string uid);
    Task<UserProfile?> GetProfileAsync();
    Task<UserProfile?> UpdateProfileAsync(UserProfile profile);
    Task<AccountDeletionPreview?> GetAccountDeletionPreviewAsync();
    Task<AccountDeletionResult> RequestAccountDeletionAsync(string currentPassword);
    void InvalidateProfileCache();
}
