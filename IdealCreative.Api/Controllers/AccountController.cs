using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using IdealCreative.Api.Data;
using IdealCreative.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IdealCreative.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class AccountController(UserManager<ApplicationUser> users) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? string.Empty;

    [HttpGet("users/profile")]
    [Authorize]
    public async Task<IActionResult> Profile(CancellationToken ct) { var user = await users.FindByIdAsync(UserId); return user is null ? Unauthorized() : Ok(ToProfile(user)); }

    [HttpPut("users/profile")]
    [Authorize]
    public async Task<IActionResult> UpdateProfile(ProfileRequest request, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(UserId); if (user is null) return Unauthorized();
        user.DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? user.DisplayName : request.DisplayName.Trim(); user.PhoneNumber = request.PhoneNumber?.Trim(); user.BirthDate = request.BirthDate; user.Street = request.Street?.Trim(); user.Number = request.Number?.Trim(); user.Neighborhood = request.Neighborhood?.Trim(); user.City = request.City?.Trim(); user.State = request.State?.Trim(); user.ZipCode = request.ZipCode?.Trim(); user.Country = request.Country?.Trim(); user.CustomerDocument = request.CustomerDocument?.Trim(); user.UpdatedAt = DateTimeOffset.UtcNow;
        var result = await users.UpdateAsync(user); if (!result.Succeeded) return BadRequest(result.Errors); return Ok(ToProfile(user));
    }

    [HttpGet("admin/users")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ListUsers([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100); var query = users.Users.AsNoTracking(); if (!string.IsNullOrWhiteSpace(search)) query = query.Where(item => item.Email!.Contains(search) || item.DisplayName.Contains(search));
        var total = await query.CountAsync(ct); var list = await query.OrderByDescending(item => item.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct); var result = new List<object>();
        foreach (var user in list) result.Add(new { uid = user.Id, id = user.Id, email = user.Email, displayName = user.DisplayName, isAdmin = await users.IsInRoleAsync(user, "Admin"), createdAt = user.CreatedAt });
        return Ok(new { items = result, pagination = new { currentPage = page, pageSize, totalItems = total, totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize)) } });
    }

    [HttpPost("admin/users/{id}/promote")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Promote(string id) => await SetAdmin(id, true);

    [HttpPost("admin/users/{id}/demote")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Demote(string id) => await SetAdmin(id, false);

    private async Task<IActionResult> SetAdmin(string id, bool isAdmin) { var user = await users.FindByIdAsync(id); if (user is null) return NotFound(); var current = await users.IsInRoleAsync(user, "Admin"); IdentityResult result = IdentityResult.Success; if (isAdmin && !current) result = await users.AddToRoleAsync(user, "Admin"); if (!isAdmin && current) result = await users.RemoveFromRoleAsync(user, "Admin"); if (!result.Succeeded) return BadRequest(result.Errors); return Ok(new { id, isAdmin }); }
    private static object ToProfile(ApplicationUser user) => new { id = user.Id, email = user.Email, displayName = user.DisplayName, phoneNumber = user.PhoneNumber, birthDate = user.BirthDate, street = user.Street, number = user.Number, neighborhood = user.Neighborhood, city = user.City, state = user.State, zipCode = user.ZipCode, country = user.Country, customerDocument = user.CustomerDocument, updatedAt = user.UpdatedAt ?? user.CreatedAt };
    public sealed record ProfileRequest(string? DisplayName, string? PhoneNumber, DateTime? BirthDate, string? Street, string? Number, string? Neighborhood, string? City, string? State, string? ZipCode, string? Country, string? CustomerDocument);
}
