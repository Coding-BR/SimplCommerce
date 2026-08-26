using Minio;
using Minio.DataModel.Args;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using IdealCreative.Api.Services;

namespace IdealCreative.Api.Controllers;

[ApiController]
[Route("api/upload")]
public sealed class StorageController(IntegrationSettingsStore integrations, IStorageClientFactory storageFactory, IdealCreative.Api.Data.AppDbContext db) : ControllerBase
{
    [HttpPost("product-image")]
    [Authorize(Roles = "Admin")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public Task<IActionResult> ProductImage(IFormFile file, CancellationToken ct)
        => IsSafeImage(file) ? Upload(file, "products/images", ct) : Task.FromResult<IActionResult>(BadRequest(new { message = "Use uma imagem JPG, PNG, WebP, GIF ou AVIF." }));

    [HttpPost("digital-file")]
    [Authorize(Roles = "Admin")]
    [RequestSizeLimit(250 * 1024 * 1024)]
    public Task<IActionResult> DigitalFile(IFormFile file, CancellationToken ct)
        => IsSafeDigitalFile(file) ? Upload(file, "products/digital", ct) : Task.FromResult<IActionResult>(BadRequest(new { message = "Envie um arquivo ZIP válido." }));

    [HttpPost("/api/storage/upload")]
    [Authorize(Roles = "Admin")]
    [RequestSizeLimit(100 * 1024 * 1024)]
    public async Task<IActionResult> DirectUpload(IFormFile file, [FromQuery] string? path, CancellationToken ct)
    {
        var prefix = NormalizePath(path, allowEmpty: true) ?? "";
        prefix = prefix.TrimEnd('/');
        return await Upload(file, string.IsNullOrEmpty(prefix) ? "uploads" : prefix, ct);
    }

    [HttpDelete("product-image")]
    [Authorize(Roles = "Admin")]
    public Task<IActionResult> DeleteImage([FromQuery] string? path, [FromQuery] string? url, CancellationToken ct) => DeleteObject(ResolveObjectPath(path, url), ct);

    [HttpDelete("digital-file")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteDigital([FromQuery] string path, CancellationToken ct)
    {
        var normalized = NormalizePath(path, allowEmpty: false);
        if (normalized is null || !normalized.StartsWith("products/digital/", StringComparison.Ordinal))
            return BadRequest(new { message = "Caminho de arquivo digital inválido." });
        if (await dbReferenceExists(normalized, ct))
            return Conflict(new { message = "O arquivo ainda está vinculado a um produto. Remova-o no cadastro do produto." });
        return await DeleteObject(normalized, ct);
    }

    [HttpDelete("digital-file/{productId:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RemoveProductDigitalFile(Guid productId, CancellationToken ct)
    {
        var product = await db.Products.SingleOrDefaultAsync(item => item.Id == productId, ct);
        if (product is null) return NotFound();
        if (string.IsNullOrWhiteSpace(product.DigitalFilePath)) return NoContent();

        var path = NormalizePath(product.DigitalFilePath, allowEmpty: false);
        if (path is null || !path.StartsWith("products/digital/", StringComparison.Ordinal))
            return Conflict(new { message = "O arquivo vinculado ao produto tem um caminho inválido." });

        var (minio, storage) = await ClientAsync(ct);
        try
        {
            await minio.RemoveObjectAsync(new RemoveObjectArgs().WithBucket(storage.Bucket).WithObject(path), ct);
        }
        catch (Minio.Exceptions.ObjectNotFoundException)
        {
            // A referência está obsoleta; ainda assim removemos o vínculo do catálogo.
        }

        product.DigitalFilePath = null;
        product.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("signed-url")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SignedUpload(SignedUploadRequest request, CancellationToken ct)
    {
        var (minio, storage) = await ClientAsync(ct); var bucket = storage.Bucket; if (!await minio.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucket), ct)) await minio.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucket), ct);
        var safeName = Path.GetFileName(request.FileName).Replace(' ', '-'); var path = $"products/digital/{Guid.NewGuid():N}-{safeName}";
        var url = await minio.PresignedPutObjectAsync(new PresignedPutObjectArgs().WithBucket(bucket).WithObject(path).WithExpiry(900));
        return Ok(new { uploadUrl = url, filePath = path });
    }

    [HttpGet("/api/storage/public")]
    [AllowAnonymous]
    public async Task<IActionResult> PublicImage([FromQuery] string path, CancellationToken ct)
    {
        var normalized = NormalizePath(path, allowEmpty: false);
        if (normalized is null || (!normalized.StartsWith("products/images/", StringComparison.Ordinal) && !normalized.StartsWith("site/logo/", StringComparison.Ordinal))) return NotFound();
        var (minio, storage) = await ClientAsync(ct);
        var memory = new MemoryStream();
        try { await minio.GetObjectAsync(new GetObjectArgs().WithBucket(storage.Bucket).WithObject(normalized).WithCallbackStream(stream => stream.CopyTo(memory)), ct); }
        catch (Minio.Exceptions.ObjectNotFoundException) { return NotFound(); }
        memory.Position = 0; return File(memory, ContentTypeFor(normalized));
    }

    [HttpGet("/api/storage/browser")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Browse([FromQuery] string? path = null, [FromQuery] int pageSize = 50, [FromQuery] string? continuationToken = null, [FromQuery] string? search = null, CancellationToken ct = default)
    {
        var (minio, storage) = await ClientAsync(ct); var bucket = storage.Bucket;
        var prefix = NormalizePath(path, allowEmpty: true) ?? "";
        if (!string.IsNullOrEmpty(prefix) && !prefix.EndsWith('/')) prefix += "/";
        pageSize = Math.Clamp(pageSize, 1, 100);
        if (!await minio.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucket), ct)) return Ok(new { currentPath = prefix, items = Array.Empty<object>(), totalItems = 0, pageSize, continuationToken = (string?)null, hasMore = false });

        var rows = new List<StorageItem>();
        await foreach (var item in minio.ListObjectsEnumAsync(new ListObjectsArgs().WithBucket(bucket).WithPrefix(prefix).WithRecursive(false), ct))
        {
            var key = item.Key;
            var unescapedKey = Uri.UnescapeDataString(key);
            var cleanKey = unescapedKey.TrimEnd('/');
            if (string.IsNullOrWhiteSpace(cleanKey) || cleanKey.Equals(prefix.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
                continue;

            var name = cleanKey.Split('/').LastOrDefault() ?? cleanKey;
            var isFolder = item.IsDir || key.EndsWith('/') || (item.Size == 0 && !name.Contains('.'));

            if (!string.IsNullOrWhiteSpace(search) && !name.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase))
                continue;

            var targetKey = isFolder ? cleanKey + "/" : key;
            rows.Add(new StorageItem(name, targetKey, isFolder, Convert.ToInt64(item.Size), null));
        }
        rows = rows.GroupBy(i => i.Key).Select(g => g.First()).OrderByDescending(item => item.IsFolder).ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToList();
        if (!string.IsNullOrWhiteSpace(continuationToken)) rows = rows.SkipWhile(item => item.Key != continuationToken).Skip(1).ToList();
        var page = rows.Take(pageSize + 1).ToList(); var hasMore = page.Count > pageSize; if (hasMore) page.RemoveAt(page.Count - 1);
        return Ok(new { currentPath = prefix, items = page, totalItems = rows.Count, pageSize, continuationToken = hasMore ? page.LastOrDefault()?.Key : null, hasMore });
    }

    [HttpGet("/api/storage/versions")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Inspect([FromQuery] string path, CancellationToken ct)
    {
        var normalized = NormalizePath(path, allowEmpty: false); if (normalized is null) return BadRequest(new { message = "Caminho inválido." });
        var (minio, storage) = await ClientAsync(ct);
        try
        {
            var stat = await minio.StatObjectAsync(new StatObjectArgs().WithBucket(storage.Bucket).WithObject(normalized), ct);
            return Ok(new { path = normalized, versionCount = 1, deleteMarkerCount = 0, totalSize = Convert.ToInt64(stat.Size), details = new[] { $"Objeto ativo · {Convert.ToInt64(stat.Size)} bytes · ETag {stat.ETag}" } });
        }
        catch (Minio.Exceptions.ObjectNotFoundException) { return NotFound(); }
    }

    [HttpDelete("/api/storage/file")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteStoredFile([FromQuery] string path, CancellationToken ct)
    {
        var normalized = NormalizePath(path, allowEmpty: false); if (normalized is null) return BadRequest(new { message = "Caminho inválido." });
        if (await dbReferenceExists(normalized, ct)) return Conflict(new { message = "O arquivo ainda está vinculado a um produto. Remova-o no cadastro do produto antes de excluir." });
        return await DeleteObject(normalized, ct);
    }

    private async Task<IActionResult> Upload(IFormFile? file, string prefix, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest(new { message = "Arquivo obrigatório." });
        var (minio, storage) = await ClientAsync(ct); var bucket = storage.Bucket;
        var safeName = Path.GetFileName(file.FileName).Replace(' ', '-');
        var objectName = $"{prefix}/{Guid.NewGuid():N}-{safeName}";
        if (!await minio.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucket), ct))
            await minio.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucket), ct);
        await using var stream = file.OpenReadStream();
        await minio.PutObjectAsync(new PutObjectArgs().WithBucket(bucket).WithObject(objectName).WithStreamData(stream).WithObjectSize(file.Length).WithContentType(ContentTypeFor(objectName, file.ContentType)), ct);
        return Ok(new { url = CreatePublicUrl(objectName, storage.PublicBaseUrl), fileName = safeName, path = objectName, filePath = objectName, fileSize = file.Length });
    }

    private async Task<IActionResult> DeleteObject(string path, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Contains("..", StringComparison.Ordinal)) return BadRequest();
        var (minio, storage) = await ClientAsync(ct); await minio.RemoveObjectAsync(new RemoveObjectArgs().WithBucket(storage.Bucket).WithObject(path), ct); return NoContent();
    }

    private static string CreatePublicUrl(string objectName, string configuredBaseUrl)
    {
        var publicBase = configuredBaseUrl.TrimEnd('/');
        return publicBase.EndsWith("/api/storage/public", StringComparison.OrdinalIgnoreCase) ? $"{publicBase}?path={Uri.EscapeDataString(objectName)}" : $"{publicBase}/{objectName}";
    }
    private string ResolveObjectPath(string? path, string? url)
    {
        if (!string.IsNullOrWhiteSpace(path)) return path;
        if (Uri.TryCreate(url, UriKind.Absolute, out var parsed))
        {
            var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(parsed.Query);
            if (query.TryGetValue("path", out var queryPath)) return queryPath.ToString();
            var knownPrefixes = new[] { "/products/", "/site/" }; var prefix = knownPrefixes.Select(value => parsed.AbsolutePath.IndexOf(value, StringComparison.Ordinal)).Where(value => value >= 0).DefaultIfEmpty(-1).Min();
            return prefix >= 0 ? Uri.UnescapeDataString(parsed.AbsolutePath[(prefix + 1)..]) : Uri.UnescapeDataString(parsed.AbsolutePath.TrimStart('/'));
        }
        return string.Empty;
    }
    private static string ContentTypeFor(string path, string? fallback = null) => Path.GetExtension(path).ToLowerInvariant() switch { ".jpg" or ".jpeg" => "image/jpeg", ".png" => "image/png", ".webp" => "image/webp", ".gif" => "image/gif", ".avif" => "image/avif", _ => fallback ?? "application/octet-stream" };
    private static bool IsSafeImage(IFormFile? file)
    {
        if (file is null || file.Length == 0 || file.Length > 10 * 1024 * 1024) return false;
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowedExtension = extension is ".jpg" or ".jpeg" or ".png" or ".webp" or ".gif" or ".avif";
        var contentType = (file.ContentType ?? string.Empty).ToLowerInvariant();
        var allowedContentType = contentType is "image/jpeg" or "image/jpg" or "image/png" or "image/webp" or "image/gif" or "image/avif";
        return allowedExtension && (allowedContentType || string.IsNullOrEmpty(contentType) || contentType == "application/octet-stream");
    }
    private static bool IsSafeDigitalFile(IFormFile? file) => file is not null && file.Length > 0 && string.Equals(Path.GetExtension(file.FileName), ".zip", StringComparison.OrdinalIgnoreCase);

    private static string? NormalizePath(string? path, bool allowEmpty)
    {
        var value = (path ?? string.Empty).Replace('\\', '/').TrimStart('/');
        if (value.Contains("..", StringComparison.Ordinal) || (!allowEmpty && string.IsNullOrWhiteSpace(value))) return null;
        return value;
    }

    private async Task<bool> dbReferenceExists(string path, CancellationToken ct)
    {
        return await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.AnyAsync(db.Products, product => product.DigitalFilePath == path || product.CoverImageUrl == path || (product.CoverImageUrl != null && product.CoverImageUrl.EndsWith("/" + path)), ct);
    }

    private async Task<(IMinioClient Client, StorageRuntimeSettings Settings)> ClientAsync(CancellationToken ct)
    {
        var settings = (await integrations.GetRuntimeAsync(ct)).Storage;
        return (storageFactory.Create(settings), settings);
    }

    private sealed record StorageItem(string Name, string Key, bool IsFolder, long Size, DateTime? LastModified);

    public sealed record SignedUploadRequest(string FileName, string ContentType = "application/octet-stream");
}
