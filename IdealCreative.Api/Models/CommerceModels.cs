using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IdealCreative.Api.Models;

[Table("Carts")]
public sealed class CartRecord
{
    [Key] public string UserId { get; set; } = string.Empty;
    public string ItemsJson { get; set; } = "[]";
    public string? CouponCode { get; set; }
    public long DiscountCents { get; set; }
    public string? ShippingZipCode { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

[Table("Coupons")]
public sealed class CouponRecord
{
    [Key] public string Code { get; set; } = string.Empty;
    public string DiscountType { get; set; } = "Percentage";
    public decimal Value { get; set; }
    public long MinPurchaseCents { get; set; }
    public DateTimeOffset? StartDate { get; set; }
    public DateTimeOffset? EndDate { get; set; }
    public int? MaxUsesGlobal { get; set; }
    public int? MaxUsesPerUser { get; set; }
    public int CurrentUsesGlobal { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

[Table("Orders")]
public sealed class OrderRecord
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public string ItemsJson { get; set; } = "[]";
    public long SubtotalCents { get; set; }
    public long DiscountCents { get; set; }
    public long ShippingCents { get; set; }
    public long TotalCents { get; set; }
    public string Status { get; set; } = "Pending";
    public string? CouponCode { get; set; }
    public string? PaymentMethod { get; set; }
    public string? ShippingAddress { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerEmail { get; set; }
    public string? CustomerPhone { get; set; }
    public string? ZipCode { get; set; }
    public string? PaymentProvider { get; set; }
    public string? PaymentIntentId { get; set; }
    public string? TransactionId { get; set; }
    public DateTimeOffset? PaidAt { get; set; }
    public string? PaymentFailureReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

[Table("AppSettings")]
public sealed class AppSettingRecord
{
    [Key] public string Key { get; set; } = string.Empty;
    public string ValueJson { get; set; } = "{}";
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

[Table("PaymentTransactions")]
public sealed class PaymentTransactionRecord
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string ProviderPaymentId { get; set; } = string.Empty;
    public string Status { get; set; } = "created";
    public long AmountCents { get; set; }
    public string? RawPayload { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}

[Table("Categories")]
public sealed class CategoryRecord
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public int Priority { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

[Table("Tags")]
public sealed class TagRecord
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

[Table("Reviews")]
public sealed class ReviewRecord
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string Comment { get; set; } = string.Empty;
    public bool IsApproved { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
