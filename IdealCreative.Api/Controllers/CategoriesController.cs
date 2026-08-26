using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using IdealCreative.Api.Data;
using IdealCreative.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace IdealCreative.Api.Controllers;

[ApiController]
[Route("api/categories")]
public sealed class CategoriesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await db.Categories.AsNoTracking().OrderBy(item => item.Priority).ThenBy(item => item.Title).ToListAsync(ct));

    [HttpGet("all")]
    [AllowAnonymous]
    public async Task<IActionResult> All(CancellationToken ct) => Ok(await db.Categories.AsNoTracking().OrderBy(item => item.Priority).ThenBy(item => item.Title).ToListAsync(ct));

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(CategoryRequest request, CancellationToken ct) { if (string.IsNullOrWhiteSpace(request.Title)) return BadRequest(); var title = request.Title.Trim(); if (await db.Categories.AnyAsync(category => category.Title.ToLower() == title.ToLower(), ct)) return Conflict(new { message = "Categoria já cadastrada." }); var item = new CategoryRecord { Title = title, ImageUrl = request.ImageName, Priority = request.Priority }; db.Categories.Add(item); await db.SaveChangesAsync(ct); return Ok(item); }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(Guid id, CategoryRequest request, CancellationToken ct) { var item = await db.Categories.FindAsync([id], ct); if (item is null) return NotFound(); item.Title = request.Title.Trim(); item.ImageUrl = request.ImageName; item.Priority = request.Priority; await db.SaveChangesAsync(ct); return Ok(item); }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) { var item = await db.Categories.FindAsync([id], ct); if (item is null) return NotFound(); await db.Products.Where(product => product.CategoryId == id).ExecuteUpdateAsync(update => update.SetProperty(product => product.CategoryId, (Guid?)null), ct); db.Categories.Remove(item); await db.SaveChangesAsync(ct); return NoContent(); }

    public sealed record CategoryRequest(string Title, string? ImageName, int Priority = 0);
}
