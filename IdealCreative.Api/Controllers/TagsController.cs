using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IdealCreative.Api.Data;
using IdealCreative.Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace IdealCreative.Api.Controllers;

[ApiController]
[Route("api/tags")]
public sealed class TagsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await db.Tags.AsNoTracking().OrderBy(item => item.Title).ToListAsync(ct));

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(TagRequest request, CancellationToken ct) { if (string.IsNullOrWhiteSpace(request.Title)) return BadRequest(); var title = request.Title.Trim(); if (await db.Tags.AnyAsync(tag => tag.Title.ToLower() == title.ToLower(), ct)) return Conflict(new { message = "Tag já cadastrada." }); var item = new TagRecord { Title = title }; db.Tags.Add(item); await db.SaveChangesAsync(ct); return Ok(item); }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(Guid id, TagRequest request, CancellationToken ct) { var item = await db.Tags.FindAsync([id], ct); if (item is null) return NotFound(); var title = request.Title.Trim(); if (string.IsNullOrWhiteSpace(title)) return BadRequest(); var oldTitle = item.Title; item.Title = title; await RewriteProductTags(oldTitle, title, ct); await db.SaveChangesAsync(ct); return Ok(item); }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { var item = await db.Tags.FindAsync([id], ct); if (item is null) return NotFound(); await RewriteProductTags(item.Title, null, ct); db.Tags.Remove(item); await db.SaveChangesAsync(ct); return NoContent(); }

    private async Task RewriteProductTags(string oldTitle, string? newTitle, CancellationToken ct)
    {
        var products = await db.Products.Where(product => EF.Functions.ILike(product.TagsJson, "%\"" + oldTitle + "\"%")).ToListAsync(ct);
        foreach (var product in products)
        {
            var tags = JsonSerializer.Deserialize<List<string>>(product.TagsJson) ?? [];
            tags = tags.Where(tag => !tag.Equals(oldTitle, StringComparison.OrdinalIgnoreCase)).ToList();
            if (!string.IsNullOrWhiteSpace(newTitle) && !tags.Contains(newTitle, StringComparer.OrdinalIgnoreCase)) tags.Add(newTitle);
            product.TagsJson = JsonSerializer.Serialize(tags); product.UpdatedAt = DateTimeOffset.UtcNow;
        }
    }

    public sealed record TagRequest(string Title);
}
