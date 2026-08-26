using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using IdealCreative.Api.Contracts;
using IdealCreative.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

namespace IdealCreative.Api.Services;

public sealed class TokenService(IConfiguration configuration, UserManager<ApplicationUser> users) : ITokenService
{
    public async Task<AuthResponse> CreateAsync(ApplicationUser user)
    {
        var issuer = configuration["Jwt:Issuer"] ?? "idealcreative.local";
        var key = configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is required.");
        var isAdmin = await users.IsInRoleAsync(user, "Admin");
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(ClaimTypes.Name, user.DisplayName),
            new(ClaimTypes.Role, isAdmin ? "Admin" : "Customer"),
            new("ic_tv", user.TokenVersion.ToString(System.Globalization.CultureInfo.InvariantCulture))
        };
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(issuer, issuer, claims, expires: DateTime.UtcNow.AddHours(8), signingCredentials: credentials);
        return new AuthResponse(new JwtSecurityTokenHandler().WriteToken(token), user.Id, user.Email ?? string.Empty, user.DisplayName, isAdmin);
    }
}
