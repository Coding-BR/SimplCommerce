using IdealCreative.Api.Contracts;
using IdealCreative.Api.Models;

namespace IdealCreative.Api.Services;

public interface ITokenService
{
    Task<AuthResponse> CreateAsync(ApplicationUser user);
}
