namespace BlazorClient.Models;

public class AuthResult
{
    public bool Success { get; set; }
    public string? Token { get; set; }
    public UserInfo? User { get; set; }
    public string? Error { get; set; }
    public string? Message { get; set; }
}

public class UserInfo
{
    public string Uid { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? DisplayName { get; set; }
    public string? PhotoURL { get; set; }
    public bool EmailVerified { get; set; }
    public bool IsAdmin { get; set; }
}
