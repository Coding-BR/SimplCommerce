using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using IdealCreative.Api.Data;
using IdealCreative.Api.Models;
using IdealCreative.Api.Services;
using Minio;
using Minio.DataModel.Args;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IdealCreative.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class DownloadsController(AppDbContext db, IConfiguration configuration, IntegrationSettingsStore integrations, IStorageClientFactory storageFactory) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? string.Empty;

    [HttpGet("downloads/{productId}")]
    [Authorize]
    public async Task<IActionResult> CustomerLink(string productId, CancellationToken ct)
    {
        var product = await FindProduct(productId, ct); if (product is null || !product.IsDigital || string.IsNullOrWhiteSpace(product.DigitalFilePath) || product.HideDigitalFromCustomer) return NotFound();
        var orders = await db.Orders.AsNoTracking().Where(order => order.UserId == UserId && order.Status != "Cancelled" && order.Status != "Pending" && order.Status != "AwaitingPayment").ToListAsync(ct); if (!orders.Any(order => ContainsProduct(order, product.Id))) return Forbid(); return Ok(Link(product, false));
    }

    [HttpGet("downloads/{productId}/admin")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AdminLink(string productId, CancellationToken ct) { var product = await FindProduct(productId, ct); return product is null || string.IsNullOrWhiteSpace(product.DigitalFilePath) ? NotFound() : Ok(Link(product, true)); }

    [HttpGet("downloads")]
    [Authorize]
    public async Task<IActionResult> MyDownloads(CancellationToken ct)
    {
        var orders = await db.Orders.AsNoTracking().Where(order => order.UserId == UserId && order.Status != "Cancelled" && order.Status != "Pending" && order.Status != "AwaitingPayment").ToListAsync(ct); var ids = orders.SelectMany(order => ReadItems(order)).Select(item => item.ProductId).Distinct().Select(value => Guid.TryParse(value, out var id) ? id : Guid.Empty).Where(id => id != Guid.Empty).ToArray(); var products = await db.Products.AsNoTracking().Where(product => ids.Contains(product.Id)).ToListAsync(ct); var items = products.Where(product => product.IsDigital && !product.HideDigitalFromCustomer && !string.IsNullOrWhiteSpace(product.DigitalFilePath)).Select(product => new { productId = product.Id, productTitle = product.Name, productImage = product.CoverImageUrl, purchaseDate = orders.Where(order => ContainsProduct(order, product.Id)).Select(order => order.CreatedAt).FirstOrDefault() }).ToList(); return Ok(new { items, pagination = new { currentPage = 1, pageSize = 100, totalItems = items.Count, totalPages = 1 } });
    }

    [HttpGet("storage/download")]
    [Authorize(Roles = "Admin")]
    public IActionResult StorageLink([FromQuery] string path) => Ok(new { url = CreateProxyUrl(path, true) });

    [HttpGet("storage/file")]
    [AllowAnonymous]
    public async Task<IActionResult> File([FromQuery] string path, [FromQuery] string token, CancellationToken ct)
    {
        if (!ValidateToken(path, token, out _)) return Unauthorized(); if (path.Contains("..", StringComparison.Ordinal)) return BadRequest();
        var storage = (await integrations.GetRuntimeAsync(ct)).Storage; var minio = storageFactory.Create(storage); var memory = new MemoryStream();
        try
        {
            await minio.GetObjectAsync(new GetObjectArgs().WithBucket(storage.Bucket).WithObject(path).WithCallbackStream(stream => stream.CopyTo(memory)), ct);
        }
        catch (Minio.Exceptions.ObjectNotFoundException)
        {
            return NotFound();
        }
        memory.Position = 0; return File(memory, "application/octet-stream", Path.GetFileName(path));
    }

    private async Task<Product?> FindProduct(string value, CancellationToken ct) => Guid.TryParse(value, out var id) ? await db.Products.FindAsync([id], ct) : await db.Products.SingleOrDefaultAsync(item => item.Slug == value, ct);
    private static bool ContainsProduct(OrderRecord order, Guid productId) => ReadItems(order).Any(item => item.ProductId == productId.ToString());
    private static List<CommerceController.StoredCartItem> ReadItems(OrderRecord order) => JsonSerializer.Deserialize<List<CommerceController.StoredCartItem>>(order.ItemsJson, new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? [];
    private object Link(Product product, bool admin) => new { downloadUrl = CreateProxyUrl(product.DigitalFilePath!, admin), expiresAt = DateTimeOffset.UtcNow.AddMinutes(15), productTitle = product.Name };
    private string CreateProxyUrl(string path, bool admin) { var exp = DateTimeOffset.UtcNow.AddMinutes(15).ToUnixTimeSeconds().ToString(); var subject = admin ? "admin" : UserId; var payload = $"{subject}|{exp}|{path}"; var key = Encoding.UTF8.GetBytes(configuration["Jwt:Key"] ?? "local-only-change-this-key-before-production-2026"); using var hmac = new HMACSHA256(key); var signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).Replace('+', '-').Replace('/', '_').TrimEnd('='); var baseUrl = configuration["Storage:ProxyBaseUrl"] ?? "http://localhost:5288"; return $"{baseUrl}/api/storage/file?path={Uri.EscapeDataString(path)}&token={Uri.EscapeDataString(Convert.ToBase64String(Encoding.UTF8.GetBytes(payload)).Replace('+', '-').Replace('/', '_').TrimEnd('=') + "." + signature)}"; }
    private bool ValidateToken(string path, string token, out string subject) { subject = string.Empty; try { var parts = token.Split('.'); if (parts.Length != 2) return false; var payload = Encoding.UTF8.GetString(Convert.FromBase64String(parts[0].Replace('-', '+').Replace('_', '/') + new string('=', (4 - parts[0].Length % 4) % 4))); var fields = payload.Split('|', 3); if (fields.Length != 3 || fields[2] != path || !long.TryParse(fields[1], out var exp) || exp < DateTimeOffset.UtcNow.ToUnixTimeSeconds()) return false; var key = Encoding.UTF8.GetBytes(configuration["Jwt:Key"] ?? "local-only-change-this-key-before-production-2026"); using var hmac = new HMACSHA256(key); var expected = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).Replace('+', '-').Replace('/', '_').TrimEnd('='); if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(parts[1]))) return false; subject = fields[0]; return true; } catch { return false; } }
}
