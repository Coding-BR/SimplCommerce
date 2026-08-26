namespace IdealCreative.Api.Contracts;

public sealed class ProductResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string FullDesc { get; init; } = string.Empty;
    public long PriceCents { get; init; }
    public decimal Price { get; init; }
    public int Stock { get; init; }
    public int Qty { get; init; }
    public bool IsDigital { get; init; }
    public bool IsPublished { get; init; }
    public string? CoverImageUrl { get; init; }
    // Compatibility name used by the Blazor storefront and legacy product forms.
    public string? ImageName { get; init; }
    public string? DigitalFilePath { get; init; }
    public bool HideDigitalFromCustomer { get; init; }
    public Guid? CategoryId { get; init; }
    public List<string> TagsArray { get; init; } = [];
    public int SalesCount { get; init; }
    public int Views { get; init; }
    public double AverageRating { get; init; }
    public int ReviewCount { get; init; }
    public List<string> Images { get; init; } = [];
}

public sealed class ProductListResponse
{
    public List<ProductResponse> Items { get; init; } = [];
    public PaginationResponse Pagination { get; init; } = new();
}

public sealed class PaginationResponse
{
    public int CurrentPage { get; init; }
    public int PageSize { get; init; }
    public int TotalPages { get; init; }
    public int TotalItems { get; init; }
}

public sealed class CreateProductRequest
{
    public string? Name { get; init; }
    public string? Title { get; init; }
    public string? Slug { get; init; }
    public string? Description { get; init; }
    public string? FullDesc { get; init; }
    public long? PriceCents { get; init; }
    public decimal? Price { get; init; }
    public int? Stock { get; init; }
    public int? Qty { get; init; }
    public bool IsDigital { get; init; }
    public bool IsPublished { get; init; } = true;
    public string? CoverImageUrl { get; init; }
    public string? ImageName { get; init; }
    public List<string>? Images { get; init; }
    public string? DigitalFilePath { get; init; }
    public bool HideDigitalFromCustomer { get; init; }
    public Guid? CategoryId { get; init; }
    public List<string>? TagsArray { get; init; }
    public string? Tags { get; init; }
}
