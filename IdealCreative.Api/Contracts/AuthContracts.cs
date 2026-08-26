namespace IdealCreative.Api.Contracts;

public sealed record RegisterRequest(string Email, string Password, string DisplayName);
public sealed record LoginRequest(string Email, string Password);
public sealed record ForgotPasswordRequest(string Email);
public sealed record ResetPasswordRequest(string Email, string Token, string Password);
public sealed record AuthResponse(string AccessToken, string UserId, string Email, string DisplayName, bool IsAdmin);
