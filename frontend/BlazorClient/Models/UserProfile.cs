
namespace BlazorClient.Models;

public class UserProfile
{
    public string Id { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? DisplayName { get; set; }

    public string? PhoneNumber { get; set; }

    public DateTime? BirthDate { get; set; }

    // Address Fields
    public string? Street { get; set; }

    public string? Number { get; set; }

    public string? Neighborhood { get; set; }

    public string? City { get; set; }

    public string? State { get; set; }

    public string? ZipCode { get; set; }
    
    public string? Country { get; set; }
    
    public string? CustomerDocument { get; set; }

    public DateTime? UpdatedAt { get; set; }
}

public sealed class AccountDeletionPreview
{
    public bool CanCompleteNow { get; set; }
    public List<AccountDeletionBlocker> BlockingOrders { get; set; } = [];
    public List<string> DataRemovedNow { get; set; } = [];
    public List<string> DataRetained { get; set; } = [];
    public string Notice { get; set; } = string.Empty;
}

public sealed class AccountDeletionBlocker
{
    public string OrderId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class AccountDeletionResult
{
    public string Status { get; set; } = string.Empty;
    public bool AccessRevoked { get; set; }
    public bool Anonymized { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<AccountDeletionBlocker> BlockingOrders { get; set; } = [];
    public bool Success { get; set; }
}
