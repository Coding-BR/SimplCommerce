using Microsoft.AspNetCore.Identity;

namespace IdealCreative.Api.Models;

public sealed class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
    public string AccountState { get; set; } = AccountStates.Active;
    public DateTimeOffset? DeletionRequestedAt { get; set; }
    public DateTimeOffset? DeactivatedAt { get; set; }
    public DateTimeOffset? AnonymizedAt { get; set; }
    public int TokenVersion { get; set; }
    public DateTimeOffset? RetentionUntil { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTime? BirthDate { get; set; }
    public string? Street { get; set; }
    public string? Number { get; set; }
    public string? Neighborhood { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? ZipCode { get; set; }
    public string? Country { get; set; }
    public string? CustomerDocument { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

public static class AccountStates
{
    public const string Active = "Active";
    public const string DeletionRequested = "DeletionRequested";
    public const string Anonymized = "Anonymized";
}
