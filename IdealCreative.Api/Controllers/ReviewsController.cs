using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using IdealCreative.Api.Data;
using IdealCreative.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IdealCreative.Api.Controllers;

[ApiController]
[Route("api/reviews")]
public sealed class ReviewsController(AppDbContext db, UserManager<ApplicationUser> users) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? string.Empty;

    [HttpGet("{productId}")]
    [AllowAnonymous]
    public async Task<IActionResult> List(string productId, [FromQuery] int pageSize = 5, CancellationToken ct = default)
    {
        if (!Guid.TryParse(productId, out var productGuid)) return BadRequest(); pageSize = Math.Clamp(pageSize, 1, 50);
        var rows = await db.Reviews.AsNoTracking().Where(item => item.ProductId == productGuid && item.IsApproved).OrderByDescending(item => item.CreatedAt).Take(pageSize).ToListAsync(ct); var items = new List<object>();
        foreach (var row in rows) { var user = await users.FindByIdAsync(row.UserId); items.Add(ToResponse(row, user?.DisplayName ?? "Cliente")); }
        return Ok(new { items, pagination = new { currentPage = 1, pageSize, totalItems = rows.Count, totalPages = 1 } });
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create(ReviewRequest request, CancellationToken ct)
    {
        if (!Guid.TryParse(request.ProductId, out var productId) || request.Rating is < 1 or > 5 || string.IsNullOrWhiteSpace(request.Comment) || request.Comment.Length > 1000) return BadRequest(new { message = "Avaliação inválida." });
        if (!await db.Products.AnyAsync(item => item.Id == productId, ct)) return NotFound(new { message = "Produto não encontrado." });
        if (await db.Reviews.AnyAsync(item => item.ProductId == productId && item.UserId == UserId, ct)) return Conflict(new { message = "Você já avaliou este produto." });
        var paidOrders = await db.Orders.AsNoTracking().Where(order => order.UserId == UserId && (order.Status == "Paid" || order.Status == "Processing" || order.Status == "Shipped" || order.Status == "Delivered")).Select(order => order.ItemsJson).ToListAsync(ct);
        if (!paidOrders.Any(itemsJson => ContainsProduct(itemsJson, productId))) return Forbid();
        var row = new ReviewRecord { ProductId = productId, UserId = UserId, Rating = request.Rating, Comment = request.Comment.Trim(), IsApproved = false }; db.Reviews.Add(row); await db.SaveChangesAsync(ct); var user = await users.FindByIdAsync(UserId); return Ok(ToResponse(row, user?.DisplayName ?? "Cliente", hidden: true));
    }

    [HttpGet("admin")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Admin([FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var rows = await db.Reviews.AsNoTracking().OrderByDescending(item => item.CreatedAt).Take(Math.Clamp(pageSize, 1, 100)).ToListAsync(ct); var items = new List<object>(); foreach (var row in rows) { var user = await users.FindByIdAsync(row.UserId); items.Add(ToResponse(row, user?.DisplayName ?? "Cliente", !row.IsApproved)); } return Ok(new { items, pagination = new { currentPage = 1, pageSize, totalItems = rows.Count, totalPages = 1 } });
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(Guid id, ReviewRequest request, CancellationToken ct) { var row = await db.Reviews.FindAsync([id], ct); if (row is null) return NotFound(); if (request.Rating is >= 1 and <= 5) row.Rating = request.Rating; if (!string.IsNullOrWhiteSpace(request.Comment)) row.Comment = request.Comment.Trim(); row.IsApproved = !request.IsHidden; await db.SaveChangesAsync(ct); return Ok(row); }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { var row = await db.Reviews.FindAsync([id], ct); if (row is null) return NotFound(); db.Reviews.Remove(row); await db.SaveChangesAsync(ct); return NoContent(); }
    private static object ToResponse(ReviewRecord row, string userName, bool hidden = false) => new { id = row.Id, productId = row.ProductId, userId = row.UserId, userName, rating = row.Rating, comment = row.Comment, createdAt = row.CreatedAt, isHidden = hidden };
    private static bool ContainsProduct(string itemsJson, Guid productId) => (JsonSerializer.Deserialize<List<CommerceController.StoredCartItem>>(itemsJson, new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? []).Any(item => item.ProductId == productId.ToString());
    public sealed record ReviewRequest(string ProductId, int Rating, string Comment, bool IsHidden = false);
}
