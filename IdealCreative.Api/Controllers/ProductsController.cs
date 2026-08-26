using IdealCreative.Api.Contracts;
using IdealCreative.Api.Data;
using IdealCreative.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IdealCreative.Api.Controllers;

[ApiController]
[Route("api/products")]
public sealed class ProductsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ProductListResponse>> List([FromQuery] int page = 1, [FromQuery] int pageSize = 24,
        [FromQuery] string? search = null, [FromQuery] Guid? categoryId = null, [FromQuery] string? tag = null, [FromQuery] string? orderBy = null,
        [FromQuery] decimal? minPrice = null, [FromQuery] decimal? maxPrice = null, [FromQuery] string? type = null, [FromQuery] bool onlyOutOfStock = false, [FromQuery] bool includeUnpublished = false, CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        if (includeUnpublished && !User.IsInRole("Admin")) return Forbid();
        var query = db.Products.AsNoTracking();
        if (!includeUnpublished) query = query.Where(product => product.IsPublished);
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(product => product.Name.ToLower().Contains(search.Trim().ToLower()));
        if (categoryId.HasValue) query = query.Where(product => product.CategoryId == categoryId.Value);
        if (!string.IsNullOrWhiteSpace(tag)) query = query.Where(product => EF.Functions.ILike(product.TagsJson, "%\"" + tag.Trim() + "\"%"));
        if (minPrice.HasValue) query = query.Where(product => product.PriceCents >= (long)Math.Round(minPrice.Value * 100));
        if (maxPrice.HasValue) query = query.Where(product => product.PriceCents <= (long)Math.Round(maxPrice.Value * 100));
        if (string.Equals(type, "digital", StringComparison.OrdinalIgnoreCase)) query = query.Where(product => product.IsDigital);
        if (string.Equals(type, "physical", StringComparison.OrdinalIgnoreCase)) query = query.Where(product => !product.IsDigital);
        if (onlyOutOfStock) query = query.Where(product => product.Stock <= 0);
        var total = await query.CountAsync(cancellationToken);
        query = orderBy?.ToLowerInvariant() switch
        {
            "sales" or "best-selling" => query.OrderByDescending(product => product.SalesCount).ThenByDescending(product => product.CreatedAt),
            "views" or "most-visited" => query.OrderByDescending(product => product.Views).ThenByDescending(product => product.CreatedAt),
            "price-asc" => query.OrderBy(product => product.PriceCents),
            "price-desc" => query.OrderByDescending(product => product.PriceCents),
            _ => query.OrderByDescending(product => product.CreatedAt)
        };
        var items = await query
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(product => new ProductResponse
            {
                Id = product.Id, Name = product.Name, Title = product.Name, Slug = product.Slug,
                Description = product.Description, FullDesc = product.Description,
                PriceCents = product.PriceCents, Price = product.PriceCents / 100m,
                Stock = product.Stock, Qty = product.Stock, IsDigital = product.IsDigital,
                IsPublished = product.IsPublished, CoverImageUrl = product.CoverImageUrl, ImageName = product.CoverImageUrl, DigitalFilePath = product.DigitalFilePath, HideDigitalFromCustomer = product.HideDigitalFromCustomer, CategoryId = product.CategoryId, SalesCount = product.SalesCount, Views = product.Views
            }).ToListAsync(cancellationToken);
        return Ok(new ProductListResponse
        {
            Items = items,
            Pagination = new PaginationResponse { CurrentPage = page, PageSize = pageSize, TotalItems = total, TotalPages = (int)Math.Ceiling(total / (double)pageSize) }
        });
    }

    [HttpGet("featured")]
    [AllowAnonymous]
    public async Task<ActionResult<ProductListResponse>> Featured(CancellationToken cancellationToken)
    {
        var monthTag = System.Globalization.CultureInfo.GetCultureInfo("pt-BR").DateTimeFormat.GetMonthName(DateTime.UtcNow.Month);
        var seasonal = db.Products.AsNoTracking().Where(product => product.IsPublished &&
            (EF.Functions.ILike(product.TagsJson, "%\"sazonal\"%") || EF.Functions.ILike(product.TagsJson, "%\"" + monthTag + "\"%")));
        if (!await seasonal.AnyAsync(cancellationToken))
            seasonal = db.Products.AsNoTracking().Where(product => product.IsPublished);
        var rows = await seasonal.OrderByDescending(product => product.UpdatedAt).Take(8).ToListAsync(cancellationToken);
        return Ok(new ProductListResponse { Items = rows.Select(product => ToResponse(product)).ToList(), Pagination = new PaginationResponse { CurrentPage = 1, PageSize = 8, TotalItems = rows.Count, TotalPages = rows.Count == 0 ? 0 : 1 } });
    }

    [HttpGet("search-index")]
    [AllowAnonymous]
    public async Task<IActionResult> SearchIndex(CancellationToken cancellationToken)
    {
        var categories = await db.Categories.AsNoTracking().ToDictionaryAsync(category => category.Id, category => category.Title, cancellationToken);
        var products = await db.Products.AsNoTracking().Where(product => product.IsPublished).OrderBy(product => product.Name).ToListAsync(cancellationToken);
        return Ok(products.Select(product =>
        {
            var tags = System.Text.Json.JsonSerializer.Deserialize<List<string>>(product.TagsJson) ?? [];
            return new { id = product.Id, title = product.Name, imageName = product.CoverImageUrl, price = product.PriceCents / 100m, currency = "BRL", keywords = string.Join(' ', new[] { product.Name, product.Description }.Concat(tags)).ToLowerInvariant(), isSubscription = false, product.IsDigital, averageRating = 0, reviewCount = 0, categoryId = product.CategoryId, categoryName = product.CategoryId.HasValue && categories.TryGetValue(product.CategoryId.Value, out var title) ? title : null, tagsArray = tags, translatedTitles = (object?)null };
        }));
    }

    [HttpGet("{slugOrId}")]
    [AllowAnonymous]
    public async Task<ActionResult<ProductResponse>> Get(string slugOrId, CancellationToken cancellationToken)
    {
        var product = Guid.TryParse(slugOrId, out var id)
            ? await db.Products.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            : await db.Products.AsNoTracking().SingleOrDefaultAsync(item => item.Slug == slugOrId, cancellationToken);
        if (product is null || (!product.IsPublished && !User.IsInRole("Admin"))) return NotFound();
        var ratings = db.Reviews.AsNoTracking().Where(review => review.ProductId == product.Id && review.IsApproved);
        var reviewCount = await ratings.CountAsync(cancellationToken); var averageRating = reviewCount == 0 ? 0 : await ratings.AverageAsync(review => (double)review.Rating, cancellationToken);
        return Ok(ToResponse(product, averageRating, reviewCount));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ProductResponse>> Create(CreateProductRequest request, CancellationToken cancellationToken)
    {
        var name = (request.Name ?? request.Title ?? string.Empty).Trim();
        var slug = string.IsNullOrWhiteSpace(request.Slug) ? Slugify(name) : request.Slug.Trim().ToLowerInvariant();
        var cents = request.PriceCents ?? (long)Math.Round((request.Price ?? 0) * 100m, MidpointRounding.AwayFromZero);
        var stock = request.Stock ?? request.Qty ?? 0;
        if (name.Length < 3 || cents < 0 || stock < 0) return BadRequest(new { message = "Nome, preço ou estoque inválido." });
        if (await db.Products.AnyAsync(product => product.Slug == slug, cancellationToken)) return Conflict(new { message = "Já existe um produto com esse slug." });
        var tags = request.TagsArray ?? (request.Tags is null ? new List<string>() : request.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList());
        var product = new Product { Name = name, Slug = slug, Description = (request.Description ?? request.FullDesc ?? string.Empty).Trim(), PriceCents = cents, Stock = stock, IsDigital = request.IsDigital, IsPublished = request.IsPublished, CoverImageUrl = request.CoverImageUrl ?? request.ImageName, DigitalFilePath = request.DigitalFilePath, HideDigitalFromCustomer = request.HideDigitalFromCustomer, CategoryId = request.CategoryId, TagsJson = System.Text.Json.JsonSerializer.Serialize(tags) };
        db.Products.Add(product);
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(Get), new { slugOrId = product.Slug }, ToResponse(product));
    }

    [HttpPut("{slugOrId}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ProductResponse>> Update(string slugOrId, CreateProductRequest request, CancellationToken cancellationToken)
    {
        var product = await Find(slugOrId, cancellationToken);
        if (product is null) return NotFound();
        var name = (request.Name ?? request.Title ?? product.Name).Trim();
        var cents = request.PriceCents ?? (long)Math.Round((request.Price ?? product.PriceCents / 100m) * 100m, MidpointRounding.AwayFromZero);
        var stock = request.Stock ?? request.Qty ?? product.Stock;
        if (name.Length < 3 || cents < 0 || stock < 0) return BadRequest(new { message = "Nome, preço ou estoque inválido." });
        product.Name = name; product.Slug = string.IsNullOrWhiteSpace(request.Slug) ? product.Slug : Slugify(request.Slug);
        product.Description = (request.Description ?? request.FullDesc ?? product.Description).Trim(); product.PriceCents = cents; product.Stock = stock;
        product.IsDigital = request.IsDigital; product.IsPublished = request.IsPublished; product.CoverImageUrl = request.CoverImageUrl ?? request.ImageName; product.DigitalFilePath = request.DigitalFilePath ?? product.DigitalFilePath; product.HideDigitalFromCustomer = request.HideDigitalFromCustomer; product.CategoryId = request.CategoryId ?? product.CategoryId; if (request.TagsArray is not null || request.Tags is not null) product.TagsJson = System.Text.Json.JsonSerializer.Serialize(request.TagsArray ?? request.Tags!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()); product.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken); return Ok(ToResponse(product));
    }

    [HttpDelete("{slugOrId}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(string slugOrId, CancellationToken cancellationToken)
    {
        var product = await Find(slugOrId, cancellationToken); if (product is null) return NotFound(); db.Products.Remove(product); await db.SaveChangesAsync(cancellationToken); return NoContent();
    }

    private async Task<Product?> Find(string slugOrId, CancellationToken cancellationToken) => Guid.TryParse(slugOrId, out var id)
        ? await db.Products.SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
        : await db.Products.SingleOrDefaultAsync(item => item.Slug == slugOrId, cancellationToken);

    private static ProductResponse ToResponse(Product product, double averageRating = 0, int reviewCount = 0) => new()
    {
        Id = product.Id, Name = product.Name, Title = product.Name, Slug = product.Slug,
        Description = product.Description, FullDesc = product.Description,
        PriceCents = product.PriceCents, Price = product.PriceCents / 100m,
        Stock = product.Stock, Qty = product.Stock, IsDigital = product.IsDigital,
        IsPublished = product.IsPublished, CoverImageUrl = product.CoverImageUrl, ImageName = product.CoverImageUrl, DigitalFilePath = product.DigitalFilePath, HideDigitalFromCustomer = product.HideDigitalFromCustomer, CategoryId = product.CategoryId, SalesCount = product.SalesCount, Views = product.Views,
        TagsArray = System.Text.Json.JsonSerializer.Deserialize<List<string>>(product.TagsJson) ?? [], AverageRating = averageRating, ReviewCount = reviewCount
    };

    private static string Slugify(string value) => new string(value.ToLowerInvariant().Select(character => char.IsLetterOrDigit(character) ? character : '-').ToArray()).Trim('-');

    [HttpPut("{id:guid}/stats")]
    [AllowAnonymous]
    public async Task<IActionResult> Stats(Guid id, CancellationToken cancellationToken) { var updated = await db.Products.Where(product => product.Id == id && product.IsPublished).ExecuteUpdateAsync(setters => setters.SetProperty(product => product.Views, product => product.Views + 1).SetProperty(product => product.UpdatedAt, _ => DateTimeOffset.UtcNow), cancellationToken); return updated == 0 ? NotFound() : NoContent(); }
}
