using System.Text.Json;
using IdealCreative.Api.Data;
using IdealCreative.Api.Contracts;
using IdealCreative.Api.Models;
using IdealCreative.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Minio;
using Minio.DataModel.Args;

namespace IdealCreative.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class SettingsController(AppDbContext db, IntegrationSettingsStore integrations, IStorageClientFactory storageFactory) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    [HttpGet("settings/public")]
    [AllowAnonymous]
    public async Task<IActionResult> Public(CancellationToken ct) { var row = await db.AppSettings.FindAsync(["site"], ct); return Ok(row is null ? new { id = "global", topBarMessage = "", contact = new { }, socialLinks = Array.Empty<object>(), features = Array.Empty<object>() } : JsonSerializer.Deserialize<JsonElement>(row.ValueJson, JsonOptions)); }
    [HttpPost("settings")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Save(JsonElement settings, CancellationToken ct) { var row = await db.AppSettings.FindAsync(["site"], ct) ?? new AppSettingRecord { Key = "site" }; row.ValueJson = settings.GetRawText(); row.UpdatedAt = DateTimeOffset.UtcNow; if (db.Entry(row).State == Microsoft.EntityFrameworkCore.EntityState.Detached) db.AppSettings.Add(row); await db.SaveChangesAsync(ct); return Ok(settings); }

    [HttpGet("settings/integrations")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Integrations(CancellationToken ct) => Ok(await integrations.GetAdminViewAsync(ct));

    [HttpPut("settings/integrations")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SaveIntegrations(IntegrationSettingsUpdateRequest request, CancellationToken ct)
    {
        try { return Ok(await integrations.SaveAsync(request, ct)); }
        catch (ArgumentException error) { return BadRequest(new { message = error.Message }); }
    }
    [HttpPost("logo/upload")]
    [Authorize(Roles = "Admin")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IActionResult> UploadLogo(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest(new { message = "Arquivo obrigatório." }); var storage = (await integrations.GetRuntimeAsync(ct)).Storage; var minio = storageFactory.Create(storage); var bucket = storage.Bucket; if (!await minio.BucketExistsAsync(new BucketExistsArgs().WithBucket(bucket), ct)) await minio.MakeBucketAsync(new MakeBucketArgs().WithBucket(bucket), ct); var path = $"site/logo/{Guid.NewGuid():N}-{Path.GetFileName(file.FileName)}"; await using var stream = file.OpenReadStream(); await minio.PutObjectAsync(new PutObjectArgs().WithBucket(bucket).WithObject(path).WithStreamData(stream).WithObjectSize(file.Length).WithContentType(file.ContentType ?? "image/png"), ct); var publicBase = storage.PublicBaseUrl.TrimEnd('/'); var url = publicBase.EndsWith("/api/storage/public", StringComparison.OrdinalIgnoreCase) ? $"{publicBase}?path={Uri.EscapeDataString(path)}" : $"{publicBase}/{path}"; var row = await db.AppSettings.FindAsync(["logo"], ct) ?? new AppSettingRecord { Key = "logo" }; row.ValueJson = JsonSerializer.Serialize(new { logoUrl = url, path }); row.UpdatedAt = DateTimeOffset.UtcNow; if (db.Entry(row).State == Microsoft.EntityFrameworkCore.EntityState.Detached) db.AppSettings.Add(row); await db.SaveChangesAsync(ct); return Ok(new { url, fileName = file.FileName, path });
    }

    [HttpGet("logo")]
    [AllowAnonymous]
    public async Task<IActionResult> Logo(CancellationToken ct) { var row = await db.AppSettings.FindAsync(["logo"], ct); if (row is null) return Ok(new { logoUrl = (string?)null }); return Ok(JsonSerializer.Deserialize<JsonElement>(row.ValueJson, JsonOptions)); }

    [HttpDelete("logo")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteLogo(CancellationToken ct) { var row = await db.AppSettings.FindAsync(["logo"], ct); if (row is not null) { try { var data = JsonSerializer.Deserialize<LogoSetting>(row.ValueJson, JsonOptions); if (!string.IsNullOrWhiteSpace(data?.Path)) { var storage = (await integrations.GetRuntimeAsync(ct)).Storage; await storageFactory.Create(storage).RemoveObjectAsync(new RemoveObjectArgs().WithBucket(storage.Bucket).WithObject(data.Path), ct); } } catch { } db.AppSettings.Remove(row); await db.SaveChangesAsync(ct); } return NoContent(); }
    private sealed record LogoSetting(string? LogoUrl, string? Path);
}
