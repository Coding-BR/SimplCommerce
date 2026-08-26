namespace IdealCreative.Api.Contracts;

public sealed record DeleteAccountRequest(string CurrentPassword);

public sealed record AccountDeletionPreview(
    bool CanCompleteNow,
    IReadOnlyList<AccountDeletionBlocker> BlockingOrders,
    IReadOnlyList<string> DataRemovedNow,
    IReadOnlyList<string> DataRetained,
    string Notice);

public sealed record AccountDeletionBlocker(Guid OrderId, string Status, DateTimeOffset CreatedAt);

public sealed record AccountDeletionResult(
    string Status,
    bool AccessRevoked,
    bool Anonymized,
    string Message,
    IReadOnlyList<AccountDeletionBlocker> BlockingOrders);
