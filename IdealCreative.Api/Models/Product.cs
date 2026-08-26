namespace IdealCreative.Api.Models;

public sealed class Product
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public long PriceCents { get; set; }
    public int Stock { get; set; }
    public bool IsDigital { get; set; }
    public bool IsPublished { get; set; }
    public string? CoverImageUrl { get; set; }
    public string? DigitalFilePath { get; set; }
    public bool HideDigitalFromCustomer { get; set; }
    public Guid? CategoryId { get; set; }
    public string TagsJson { get; set; } = "[]";
    public int Views { get; set; }
    public int SalesCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
