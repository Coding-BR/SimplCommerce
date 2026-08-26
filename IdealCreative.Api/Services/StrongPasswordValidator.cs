using Microsoft.AspNetCore.Identity;

namespace IdealCreative.Api.Services;

public sealed class StrongPasswordValidator<TUser> : IPasswordValidator<TUser> where TUser : class
{
    private static readonly HashSet<string> BlockedPasswords = new(StringComparer.OrdinalIgnoreCase)
    {
        "123456789012345", "passwordpassword", "senha1234567890", "qwertyuiopasdfg",
        "idealcreative", "idealcreative123", "adminadminadmin", "letmeinletmeinlet"
    };

    public Task<IdentityResult> ValidateAsync(UserManager<TUser> manager, TUser user, string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
            return Task.FromResult(IdentityResult.Failed(new IdentityError { Code = "PasswordRequired", Description = "A senha é obrigatória." }));

        if (password.Length > 128)
            return Task.FromResult(IdentityResult.Failed(new IdentityError { Code = "PasswordTooLong", Description = "A senha deve ter no máximo 128 caracteres." }));

        if (BlockedPasswords.Contains(password.Trim()))
            return Task.FromResult(IdentityResult.Failed(new IdentityError { Code = "PasswordCompromised", Description = "Esta senha é muito comum. Escolha uma frase-senha diferente." }));

        return Task.FromResult(IdentityResult.Success);
    }
}
