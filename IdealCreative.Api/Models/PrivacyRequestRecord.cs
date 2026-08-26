using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IdealCreative.Api.Models;

[Table("PrivacyRequests")]
public sealed class PrivacyRequestRecord
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public string Type { get; set; } = "AccountDeletion";
    public string Status { get; set; } = "Requested";
    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ProcessedAt { get; set; }
    public string? BlockingReason { get; set; }
    public string LegalBasis { get; set; } = "LGPD-Art16";
    public DateTimeOffset? RetentionUntil { get; set; }
    public string? Notes { get; set; }
}
