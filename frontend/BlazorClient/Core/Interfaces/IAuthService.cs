using BlazorClient.Models;

namespace BlazorClient.Core.Interfaces;

public interface IAuthService
{
    Task<AuthResult> SignInWithEmail(string email, string password);
    Task<AuthResult> RegisterWithEmail(string email, string password, string displayName);
    Task<AuthResult> RequestPasswordReset(string email);
    Task<AuthResult> ResetPassword(string email, string token, string password);
    Task SignOut();
    Task<string?> GetCurrentUserToken();
    Task<UserInfo?> GetCurrentUser();
}
