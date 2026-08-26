using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace BlazorClient.Models;

public class UserLocale
{
    [JsonPropertyName("language")]
    public string Language { get; set; } = "pt-BR";
    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "BRL";
    [JsonPropertyName("currency_symbol")]
    public string CurrencySymbol { get; set; } = "R$";
    [JsonPropertyName("country_code")]
    public string CountryCode { get; set; } = "BR";
}

public class Product
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? FullDesc { get; set; }
    public double Price { get; set; }
    // Runtime properties
    public decimal? DisplayPrice { get; set; }
    public decimal? DisplayOldPrice { get; set; }
    public double? OldPrice { get; set; }
    public string DisplayCurrency { get; set; } = "BRL";
    public string Currency => DisplayCurrency; // Alias for compatibility
    public string? ImageName { get; set; }
    [JsonPropertyName("coverImageUrl")]
    public string? CoverImageUrl
    {
        set
        {
            if (!string.IsNullOrWhiteSpace(value)) ImageName = value;
        }
    }
    
    /// <summary>
    /// Returns the translated title based on the client's locale.
    /// Falls back to Title if translation is not available.
    /// </summary>
    public string GetTranslatedTitle(string locale)
    {
        if (Translations == null) return Title;

        // 1. Try exact match (e.g. "pt-BR")
        if (Translations.TryGetValue(locale, out var exactMatch) && !string.IsNullOrEmpty(exactMatch.Title))
        {
            return exactMatch.Title;
        }

        // 2. Try parent culture if applicable (e.g. "pt")
        var dashIndex = locale.IndexOf('-');
        if (dashIndex > 0)
        {
            var parentLocale = locale.Substring(0, dashIndex);
            if (Translations.TryGetValue(parentLocale, out var parentMatch) && !string.IsNullOrEmpty(parentMatch.Title))
            {
                return parentMatch.Title;
            }
        }
        return Title;
    }

    /// <summary>
    /// Array de URLs de imagens adicionais do produto
    /// </summary>
    private List<string> _images = new();
    public List<string> Images 
    { 
        get => _images; 
        set => _images = value ?? new List<string>(); 
    }
    
    public int Qty { get; set; }
    public string? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public DateTime? CreatedAt { get; set; }
    public string? VideoUrl { get; set; }
    public string? DownloadUrl { get; set; }
    public string? Tags { get; set; }
    public List<string> TagsArray { get; set; } = new();
    public bool IsSubscription { get; set; }
    public string? PayPalPlanId { get; set; }
    public string? RecurringInterval { get; set; }
    public int DurationMonths { get; set; }
    public bool IsDigital { get; set; }
    public string? DigitalFilePath { get; set; }
    /// <summary>
    /// If true, the digital file is hidden from customers (for internal/admin use only)
    /// </summary>
    public bool HideDigitalFromCustomer { get; set; }
    public int SalesCount { get; set; }
    public string? TelegramGroupId { get; set; }
    
    // Fiscal Fields
    public string? Ncm { get; set; }
    public string? Cest { get; set; }
    public int Origem { get; set; }
    public string? Gtin { get; set; }
    public string Unidade { get; set; } = "UN";

    // Shipping Dimensions
    public int Width { get; set; }
    public int Height { get; set; }
    public int Length { get; set; }
    public double Weight { get; set; }

    public Dictionary<string, ProductTranslation>? Translations { get; set; }
    
    // Reviews
    public double AverageRating { get; set; }
    public int ReviewCount { get; set; }

    /// <summary>
    /// If true, skip automatic translation for this product
    /// </summary>
    public bool SkipTranslation { get; set; } = false;
}

public class ProductTranslation
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? FullDesc { get; set; }
    public bool ManualOverride { get; set; }
}


public class HolidayConfig
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Tag { get; set; } = string.Empty;
    public int StartDay { get; set; }
    public int StartMonth { get; set; }
    public int EndDay { get; set; }
    public int EndMonth { get; set; }
}

public class CreateProductDto
{
    [Required(ErrorMessage = "O título é obrigatório.")]
    [StringLength(200, MinimumLength = 3, ErrorMessage = "O título deve ter entre 3 e 200 caracteres.")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "O preço é obrigatório.")]
    [Range(0.01, double.MaxValue, ErrorMessage = "O preço deve ser maior que zero.")]
    public double Price { get; set; }

    public string? Description { get; set; }
    public string? FullDesc { get; set; }
    public int Qty { get; set; } = 0;
    public string? ImageName { get; set; }
    
    /// <summary>
    /// Array de URLs de imagens adicionais do produto
    /// </summary>
    public List<string>? Images { get; set; }
    
    public string? CategoryId { get; set; }
    public string? VideoUrl { get; set; }
    public string? DownloadUrl { get; set; }
    public string? Tags { get; set; }
    public bool IsSubscription { get; set; }
    public string? PayPalPlanId { get; set; }
    public string? RecurringInterval { get; set; }
    public int DurationMonths { get; set; } = 1;
    public bool IsDigital { get; set; }
    public string? DigitalFilePath { get; set; }
    public bool HideDigitalFromCustomer { get; set; }
    public string? TelegramGroupId { get; set; }

    // Fiscal Fields (Nuvem Fiscal)
    public string? Ncm { get; set; }
    public string? Cest { get; set; }
    public int Origem { get; set; }
    public string? Gtin { get; set; }
    public string Unidade { get; set; } = "UN";



    // Shipping Dimensions
    public int Width { get; set; }
    public int Height { get; set; }
    public int Length { get; set; }
    public double Weight { get; set; }

    /// <summary>
    /// If true, skip automatic translation for this product
    /// </summary>
    public bool SkipTranslation { get; set; } = false;

    public Dictionary<string, ProductTranslation>? Translations { get; set; }
}

public class UpdateProductDto
{
    [Required]
    public string Id { get; set; } = string.Empty;
    [Required(ErrorMessage = "O título é obrigatório.")]
    public string Title { get; set; } = string.Empty;
    [Required(ErrorMessage = "O preço é obrigatório.")]
    public double Price { get; set; }
    public string? Description { get; set; }
    public string? FullDesc { get; set; }
    public int Qty { get; set; }
    public string? ImageName { get; set; }
    
    /// <summary>
    /// Lista de URLs de imagens adicionais
    /// </summary>
    public List<string>? Images { get; set; }
    
    public string? CategoryId { get; set; }
    public string? VideoUrl { get; set; }
    public string? DownloadUrl { get; set; }
    public string? Tags { get; set; }
    public bool IsSubscription { get; set; }
    public string? PayPalPlanId { get; set; }
    public string? RecurringInterval { get; set; }
    public int DurationMonths { get; set; }
    public bool IsDigital { get; set; }
    public string? DigitalFilePath { get; set; }
    public bool HideDigitalFromCustomer { get; set; }
    public string? TelegramGroupId { get; set; }

    // Fiscal Fields (Nuvem Fiscal)
    public string? Ncm { get; set; }
    public string? Cest { get; set; }
    public int Origem { get; set; }
    public string? Gtin { get; set; }
    public string Unidade { get; set; } = "UN";



    // Shipping Dimensions
    public int Width { get; set; }
    public int Height { get; set; }
    public int Length { get; set; }
    public double Weight { get; set; }

    /// <summary>
    /// If true, skip automatic translation for this product
    /// </summary>
    public bool SkipTranslation { get; set; } = false;

    public Dictionary<string, ProductTranslation>? Translations { get; set; }
}

public record PaginationData
{
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public int TotalItems { get; set; }
    public string? LastDocId { get; set; }
    public string? NextPageToken { get; set; } // Added for cursor pagination

    public PaginationData() { }

    public PaginationData(int currentPage, int pageSize, int totalPages, int totalItems, string? lastDocId, string? nextPageToken = null)
    {
        CurrentPage = currentPage;
        PageSize = pageSize;
        TotalPages = totalPages;
        TotalItems = totalItems;
        LastDocId = lastDocId;
        NextPageToken = nextPageToken;
    }
}

public class ProductListResponse
{
    public List<Product> Items { get; set; } = new();
    public PaginationData Pagination { get; set; } = new();
}

    public class Category
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? ImageName { get; set; }
        public int Priority { get; set; }
        public Dictionary<string, CategoryTranslation>? Translations { get; set; }

    /// <summary>
    /// Returns the translated title based on the client's locale.
    /// Falls back to Title if translation is not available.
    /// </summary>
    public string GetTranslatedTitle(string locale)
    {
        if (Translations == null) return Title;

        // 1. Try exact match (e.g. "pt-BR")
        if (Translations.TryGetValue(locale, out var exactMatch) && !string.IsNullOrEmpty(exactMatch.Title))
        {
            return exactMatch.Title;
        }

        // 2. Try parent culture if applicable (e.g. "pt")
        var dashIndex = locale.IndexOf('-');
        if (dashIndex > 0)
        {
            var parentLocale = locale.Substring(0, dashIndex);
            if (Translations.TryGetValue(parentLocale, out var parentMatch) && !string.IsNullOrEmpty(parentMatch.Title))
            {
                return parentMatch.Title;
            }
        }
        return Title;
    }
}

public class CategoryTranslation
{
    public string? Title { get; set; }
    public bool ManualOverride { get; set; }
}

public class Tag
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public Dictionary<string, TagTranslation>? Translations { get; set; }

    /// <summary>
    /// Returns the translated title based on the client's locale.
    /// Falls back to Title if translation is not available.
    /// </summary>
    public string GetTranslatedTitle(string locale)
    {
        // 1. Try exact match (e.g. "pt-BR")
        if (Translations != null && Translations.TryGetValue(locale, out var exactMatch) && !string.IsNullOrEmpty(exactMatch.Title))
        {
            return exactMatch.Title;
        }

        // 2. Try parent culture if applicable (e.g. "pt")
        var dashIndex = locale.IndexOf('-');
        if (Translations != null && dashIndex > 0)
        {
            var parentLocale = locale.Substring(0, dashIndex);
            if (Translations.TryGetValue(parentLocale, out var parentMatch) && !string.IsNullOrEmpty(parentMatch.Title))
            {
                return parentMatch.Title;
            }
        }
        return Title;
    }
}

public class TagTranslation
{
    public string? Title { get; set; }
    public bool ManualOverride { get; set; }
}

public class CreateCategoryDto
{
    [Required(ErrorMessage = "O nome da categoria é obrigatório.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "O nome da categoria deve ter pelo menos 2 caracteres.")]
    public string Title { get; set; } = string.Empty;
    public string? ImageName { get; set; }
    public int Priority { get; set; }
    public Dictionary<string, CategoryTranslation>? Translations { get; set; }
}

public class UpdateCategoryDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? ImageName { get; set; }
    public int Priority { get; set; }
    public Dictionary<string, CategoryTranslation>? Translations { get; set; }
}

public class CreateTagDto
{
    public string Title { get; set; } = string.Empty;
    public Dictionary<string, TagTranslation>? Translations { get; set; }
}

public class UpdateTagDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public Dictionary<string, TagTranslation>? Translations { get; set; }
}


public record UploadImageResponse(string Url, string FileName, string Path);

/// <summary>
/// Response for digital file upload (ZIP)
/// </summary>
public class DigitalFileUploadResponse
{
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
}

/// <summary>
/// Response for download link generation with expiration
/// </summary>
public class DownloadLinkResponse
{
    public string DownloadUrl { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public string ProductTitle { get; set; } = string.Empty;
}

/// <summary>
/// Information about a digital product the user has purchased
/// </summary>
public class DigitalPurchaseInfo
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductTitle { get; set; } = string.Empty;
    public string? ProductImage { get; set; }
    public DateTime PurchaseDate { get; set; }
}

public class DigitalDownloadListResponse
{
    public List<DigitalPurchaseInfo> Items { get; set; } = new();
    // Default constructor for deserialization
    public DigitalDownloadListResponse() { }
    
    public DigitalDownloadListResponse(List<DigitalPurchaseInfo> items, PaginationData pagination)
    {
        Items = items;
        Pagination = pagination;
    }
    public PaginationData Pagination { get; set; } = new();
}

public class ProductSearchIndexDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? ImageName { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; } = "BRL";
    public string Keywords { get; set; } = string.Empty;
    public bool IsSubscription { get; set; }
    public bool IsDigital { get; set; }
    public double AverageRating { get; set; }
    public int ReviewCount { get; set; }
    public string? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public List<string> TagsArray { get; set; } = new();
    public Dictionary<string, string>? TranslatedTitles { get; set; }

    public string GetTranslatedTitle(string locale)
    {
        if (TranslatedTitles == null) return Title;

        // 1. Try exact match (e.g. "pt-BR")
        if (TranslatedTitles.TryGetValue(locale, out var exactMatch) && !string.IsNullOrEmpty(exactMatch))
        {
            return exactMatch;
        }

        // 2. Try parent culture
        var dashIndex = locale.IndexOf('-');
        if (dashIndex > 0)
        {
            var parentLocale = locale.Substring(0, dashIndex);
            if (TranslatedTitles.TryGetValue(parentLocale, out var parentMatch) && !string.IsNullOrEmpty(parentMatch))
            {
                return parentMatch;
            }
        }
        return Title;
    }
}

public class Cart
{
    public string Id { get; set; } = string.Empty; // UserId
    public List<CartItem> Items { get; set; } = new List<CartItem>();
    public DateTime? UpdatedAt { get; set; }
    
    public string? CouponCode { get; set; }
    public double DiscountAmount { get; set; }

    public string? ShippingZipCode { get; set; }

    public double SubTotal => Items.Sum(i => i.Price * i.Quantity);
    public double Total => Math.Max(0, SubTotal - DiscountAmount);
    
    // Runtime properties
    public decimal? DisplaySubTotal { get; set; }
    public decimal? DisplayTotal { get; set; }
    public decimal? DisplayDiscountAmount { get; set; }
    public string DisplayCurrency { get; set; } = "BRL";
}

public class CartItem
{
    public string ProductId { get; set; } = string.Empty;
    public string ProductTitle { get; set; } = string.Empty;
    public string? ProductImage { get; set; }
    public double Price { get; set; }
    // Runtime properties
    public decimal? DisplayPrice { get; set; }
    public string DisplayCurrency { get; set; } = "BRL";
    public int Quantity { get; set; }
    public string? Color { get; set; }
    public string? Size { get; set; }
    
    // Flags para regras de checkout
    public bool IsSubscription { get; set; }
    public bool IsDigital { get; set; }
    
    // Shipping Dimensions copy
    public int Width { get; set; }
    public int Height { get; set; }
    public int Length { get; set; }
    public double Weight { get; set; }
    
    // Shipping option selected by user
    public int? SelectedShippingServiceId { get; set; }
    public string? SelectedShippingName { get; set; }
    public string? SelectedShippingCompany { get; set; }
    public decimal? SelectedShippingPrice { get; set; }
    public int? SelectedShippingDeliveryTime { get; set; }
    public string? SelectedShippingDescription { get; set; }
    
    public double TotalPrice => Price * Quantity;
}


public class ApplyCouponDto
{
    public string Code { get; set; } = string.Empty;
}

public class OrderDto
{
    public string? PaymentMethod { get; set; }
    public string? ShippingAddress { get; set; }
    
    // Customer data
    public string? CustomerName { get; set; }
    public string? CustomerEmail { get; set; }
    public string? CustomerPhone { get; set; }
    public string? CustomerDocument { get; set; }
    
    // Detailed address
    public string? Street { get; set; }
    public string? Number { get; set; }
    public string? Neighborhood { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }
    public string? Country { get; set; }
}

public class Order
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public List<CartItem> Items { get; set; } = new();
    public double SubTotal { get; set; }
    public double DiscountAmount { get; set; }
    public string? CouponCode { get; set; }
    public double Total { get; set; }
    public string Status { get; set; } = "Pending";
    public string? PaymentMethod { get; set; }
    public string? ShippingAddress { get; set; }
    public DateTime? CreatedAt { get; set; }
    
    // Customer data
    public string? CustomerName { get; set; }
    public string? CustomerEmail { get; set; }
    public string? CustomerPhone { get; set; }
    public string? CustomerDocument { get; set; }
    
    // Detailed address
    public string? Street { get; set; }
    public string? Number { get; set; }
    public string? Neighborhood { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }
    public string? Country { get; set; }
    
    // Payment transaction fields
    [JsonPropertyName("paymentProvider")]
    public string? PaymentProvider { get; set; }        // "PayPal", "Stripe", etc.
    
    [JsonPropertyName("paymentIntentId")]
    public string? PaymentIntentId { get; set; }        // Provider's order/intent ID before capture
    
    [JsonPropertyName("transactionId")]
    public string? TransactionId { get; set; }          // Provider's transaction ID after capture (e.g., PayPal Transaction ID)
    
    [JsonPropertyName("paidAt")]
    public DateTime? PaidAt { get; set; }               // Payment date/time
    
    [JsonPropertyName("paymentFailureReason")]
    public string? PaymentFailureReason { get; set; }   // Failure reason if payment failed
    
    [JsonPropertyName("refundedAt")]
    public DateTime? RefundedAt { get; set; }           // Refund date/time
    
    [JsonPropertyName("refundAmount")]
    public double? RefundAmount { get; set; }           // Refund amount
    
    // Shipping / Tracking fields (Melhor Envio)
    [JsonPropertyName("shippingLabelId")]
    public string? ShippingLabelId { get; set; }
    
    [JsonPropertyName("trackingCode")]
    public string? TrackingCode { get; set; }
    
    [JsonPropertyName("melhorEnvioTracking")]
    public string? MelhorEnvioTracking { get; set; }
    
    [JsonPropertyName("shippingStatus")]
    public string? ShippingStatus { get; set; }
    
    [JsonPropertyName("shippingProtocol")]
    public string? ShippingProtocol { get; set; }
    
    [JsonPropertyName("carrierName")]
    public string? CarrierName { get; set; }
    
    [JsonPropertyName("carrierService")]
    public string? CarrierService { get; set; }
    
    [JsonPropertyName("shippingCost")]
    public double? ShippingCost { get; set; }
    
    [JsonPropertyName("estimatedDeliveryDate")]
    public DateTime? EstimatedDeliveryDate { get; set; }
    
    [JsonPropertyName("shippedAt")]
    public DateTime? ShippedAt { get; set; }
    
    [JsonPropertyName("labelUrl")]
    public string? LabelUrl { get; set; }

}

public class Coupon
{
    public string Code { get; set; } = string.Empty;
    public string DiscountType { get; set; } = "Percentage";
    public double Value { get; set; }
    public double MinPurchaseAmount { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsRecurring { get; set; }
    public int? MaxUsesGlobal { get; set; }
    public int? MaxUsesPerUser { get; set; }
    public int CurrentUsesGlobal { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? CreatedAt { get; set; }
}

public class CreateCouponDto
{
    [Required]
    public string Code { get; set; } = string.Empty;
    [Required]
    public string DiscountType { get; set; } = "Percentage";
    [Required]
    public double Value { get; set; }
    public double MinPurchaseAmount { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsRecurring { get; set; }
    public int? MaxUsesGlobal { get; set; }
    public int? MaxUsesPerUser { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdateCouponDto
{
    public string? DiscountType { get; set; }
    public double? Value { get; set; }
    public double? MinPurchaseAmount { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool? IsRecurring { get; set; }
    public int? MaxUsesGlobal { get; set; }
    public int? MaxUsesPerUser { get; set; }
    public bool? IsActive { get; set; }
}

public class UserDto
{
    public string Uid { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? DisplayName { get; set; }
    public bool IsAdmin { get; set; }
}

public class UserListResponse
{
    public List<UserDto> Items { get; set; } = new();
    public PaginationData Pagination { get; set; } = new();
}

public class SiteSettings
{
    public string Id { get; set; } = "global";
    public ContactInfo Contact { get; set; } = new();
    public string TopBarMessage { get; set; } = "";
    public List<SocialLink> SocialLinks { get; set; } = new();
    public List<FeatureItem> Features { get; set; } = new();
    public string? LogoUrl { get; set; }
    public bool IsMelhorEnvioEnabled { get; set; } = true;

    // Traduções automáticas para múltiplos idiomas
    public Dictionary<string, SettingsTranslation> Translations { get; set; } = new();
}

public class SettingsTranslation
{
    public string TopBarMessage { get; set; } = string.Empty;
    public List<FeatureTranslation> Features { get; set; } = new();
}

public class FeatureTranslation
{
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
}

public class ContactInfo
{
    public string Address { get; set; } = "";
    public string Hotline { get; set; } = "";
    public string Email { get; set; } = "";
}

public class SocialLink
{
    public string Network { get; set; } = string.Empty;
    public string IconClass { get; set; } = string.Empty;
    public string Url { get; set; } = "#";
    public bool IsEnabled { get; set; } = true;
}

public class FeatureItem
{
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string IconClass { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
}

// Payment models
public class PaymentInitResponse
{
    public bool Success { get; set; }
    public string PaymentId { get; set; } = string.Empty;
    public string? ApprovalUrl { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string? Error { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class PaymentCaptureResponse
{
    public bool Success { get; set; }
    public string? TransactionId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public string? Error { get; set; }
}

public class SignedUrlRequest
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/zip";

    public SignedUrlRequest() { }
    public SignedUrlRequest(string fileName, string contentType)
    {
        FileName = fileName;
        ContentType = contentType;
    }
}

public class SignedUrlResponse
{
    public string UploadUrl { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
}

// Shipping DTOs
public class ShippingQuoteResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal CustomPrice { get; set; }
    public decimal Discount { get; set; }
    public string Currency { get; set; } = "R$";
    public int DeliveryTime { get; set; } // Days
    public int? DeliveryRange { get; set; }
    public string? Description { get; set; }
    public string? Error { get; set; }
    public List<ShippingPackage>? Packages { get; set; }
}

public class ShippingPackage
{
    public decimal Price { get; set; }
    public decimal Discount { get; set; }
    public string Format { get; set; } = "box";
    public int Weight { get; set; }
    public int InsuranceValue { get; set; }
    public ShippingDimensions? Dimensions { get; set; }
}

public class ShippingDimensions
{
    public int Height { get; set; }
    public int Width { get; set; }
    public int Length { get; set; }
}

public class CalculateShippingRequest
{
    public string ToZipCode { get; set; } = string.Empty;
    public List<ShippingProductInfo> Products { get; set; } = new();
}

public class ShippingProductInfo
{
    public string Id { get; set; } = string.Empty;
    public int Width { get; set; } // cm
    public int Height { get; set; } // cm
    public int Length { get; set; } // cm
    public decimal Weight { get; set; } // kg
    public decimal InsuranceValue { get; set; } // R$
    public int Quantity { get; set; } = 1;
}

public class GenerateShipmentRequest
{
    public string OrderId { get; set; } = string.Empty;
    public int ServiceId { get; set; }
    public decimal InsuranceValue { get; set; }
}

public class ShipmentResponse
{
    public string Id { get; set; } = string.Empty;
    public string Protocol { get; set; } = string.Empty;
    public string? TrackingCode { get; set; }
    public string? MelhorEnvioTracking { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public DateTime? DeliveryEstimate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? LabelUrl { get; set; }
    public string? Error { get; set; }
}

public class TrackingResponse
{
    public string Id { get; set; } = string.Empty;
    public string Protocol { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? TrackingCode { get; set; }
    public string? MelhorEnvioTracking { get; set; }
    public DateTime? PostedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public List<TrackingEvent>? Events { get; set; }
}

public class TrackingEvent
{
    public DateTime Date { get; set; }
    public string Status { get; set; } = string.Empty;

    public string? Description { get; set; }
    public string? Location { get; set; }
}

