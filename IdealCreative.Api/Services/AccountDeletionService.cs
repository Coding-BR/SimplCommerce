using IdealCreative.Api.Contracts;
using IdealCreative.Api.Data;
using IdealCreative.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace IdealCreative.Api.Services;

public sealed class AccountDeletionService(
    AppDbContext db,
    UserManager<ApplicationUser> users,
    ILogger<AccountDeletionService> logger)
{
    private static readonly string[] BlockingStatuses = ["Pending", "AwaitingPayment", "Processing", "Shipped"];
    private static readonly string[] PersonalFieldsRemoved =
    [
        "Acesso, senha e sessões", "Carrinho", "Perfil, telefone, endereço e documento", "Avaliações e comentários"
    ];
    private static readonly string[] TransactionalFieldsRetained =
    [
        "Identificador técnico do pedido", "Itens, valores, status e data", "Referência mínima de pagamento"
    ];

    public async Task<AccountDeletionPreview> PreviewAsync(string userId, CancellationToken ct)
    {
        var blockers = await GetBlockersAsync(userId, ct);
        return new AccountDeletionPreview(
            blockers.Count == 0,
            blockers,
            PersonalFieldsRemoved,
            TransactionalFieldsRetained,
            blockers.Count == 0
                ? "Seu acesso será encerrado e os dados elegíveis serão anonimizados. O histórico comercial mínimo será preservado conforme a política de retenção."
                : "Seu acesso será encerrado, mas a anonimização final aguardará a conclusão ou cancelamento dos pedidos em andamento.");
    }

    public async Task<AccountDeletionResult> RequestAsync(ApplicationUser user, CancellationToken ct)
    {
        if (!string.Equals(user.AccountState, AccountStates.Active, StringComparison.OrdinalIgnoreCase))
            return new AccountDeletionResult(user.AccountState, true, string.Equals(user.AccountState, AccountStates.Anonymized, StringComparison.OrdinalIgnoreCase), "Esta conta já possui uma solicitação de exclusão ou foi encerrada.", []);

        var now = DateTimeOffset.UtcNow;
        var blockers = await GetBlockersAsync(user.Id, ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        user.TokenVersion++;
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        user.DeletionRequestedAt = now;
        user.DeactivatedAt = now;

        var request = new PrivacyRequestRecord
        {
            UserId = user.Id,
            RequestedAt = now,
            RetentionUntil = now.AddYears(5),
            Status = blockers.Count == 0 ? "Processing" : "Blocked",
            BlockingReason = blockers.Count == 0 ? null : BlockerDescription(blockers),
            Notes = "Solicitação confirmada pelo titular autenticado."
        };
        db.PrivacyRequests.Add(request);

        if (blockers.Count > 0)
        {
            user.AccountState = AccountStates.DeletionRequested;
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            logger.LogInformation("Conta {UserId} desativada; exclusão aguarda {Count} pedido(s) em andamento.", user.Id, blockers.Count);
            return new AccountDeletionResult(AccountStates.DeletionRequested, true, false, "Seu acesso foi encerrado. A anonimização será concluída após os pedidos em andamento serem finalizados ou cancelados.", blockers);
        }

        await AnonymizeAsync(user, request, now, ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        logger.LogInformation("Conta {UserId} foi anonimizada após solicitação do titular.", user.Id);
        return new AccountDeletionResult(AccountStates.Anonymized, true, true, "Sua conta foi encerrada e os dados elegíveis foram anonimizados.", []);
    }

    public async Task<int> FinalizePendingAsync(CancellationToken ct)
    {
        var userIds = await db.Users.AsNoTracking()
            .Where(user => user.AccountState == AccountStates.DeletionRequested)
            .Select(user => user.Id)
            .Take(100)
            .ToListAsync(ct);
        var completed = 0;

        foreach (var userId in userIds)
        {
            var blockers = await GetBlockersAsync(userId, ct);
            if (blockers.Count > 0) continue;

            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var user = await db.Users.SingleOrDefaultAsync(row => row.Id == userId && row.AccountState == AccountStates.DeletionRequested, ct);
            if (user is null)
            {
                await transaction.RollbackAsync(ct);
                continue;
            }

            var request = await db.PrivacyRequests
                .Where(row => row.UserId == userId && row.Type == "AccountDeletion" && row.Status == "Blocked")
                .OrderByDescending(row => row.RequestedAt)
                .FirstOrDefaultAsync(ct);
            if (request is null)
            {
                await transaction.RollbackAsync(ct);
                continue;
            }

            await AnonymizeAsync(user, request, DateTimeOffset.UtcNow, ct);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            completed++;
            logger.LogInformation("Solicitação pendente de exclusão da conta {UserId} foi concluída.", userId);
        }

        return completed;
    }

    private async Task<List<AccountDeletionBlocker>> GetBlockersAsync(string userId, CancellationToken ct) => await db.Orders.AsNoTracking()
        .Where(order => order.UserId == userId && BlockingStatuses.Contains(order.Status))
        .OrderBy(order => order.CreatedAt)
        .Select(order => new AccountDeletionBlocker(order.Id, order.Status, order.CreatedAt))
        .ToListAsync(ct);

    private async Task AnonymizeAsync(ApplicationUser user, PrivacyRequestRecord request, DateTimeOffset now, CancellationToken ct)
    {
        var anonymousEmail = $"deleted-{user.Id.Replace("-", string.Empty, StringComparison.Ordinal)}@invalid.local";
        user.AccountState = AccountStates.Anonymized;
        user.AnonymizedAt = now;
        user.RetentionUntil = request.RetentionUntil ?? now.AddYears(5);
        user.DisplayName = "Cliente removido";
        user.Email = anonymousEmail;
        user.NormalizedEmail = anonymousEmail.ToUpperInvariant();
        user.UserName = anonymousEmail;
        user.NormalizedUserName = anonymousEmail.ToUpperInvariant();
        user.PhoneNumber = null;
        user.PhoneNumberConfirmed = false;
        user.EmailConfirmed = false;
        user.BirthDate = null;
        user.Street = null;
        user.Number = null;
        user.Neighborhood = null;
        user.City = null;
        user.State = null;
        user.ZipCode = null;
        user.Country = null;
        user.CustomerDocument = null;
        user.PasswordHash = users.PasswordHasher.HashPassword(user, Guid.NewGuid().ToString("N"));
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        user.UpdatedAt = now;

        await db.Carts.Where(cart => cart.UserId == user.Id).ExecuteDeleteAsync(ct);
        await db.Reviews.Where(review => review.UserId == user.Id).ExecuteDeleteAsync(ct);
        await db.Orders.Where(order => order.UserId == user.Id).ExecuteUpdateAsync(update => update
            .SetProperty(order => order.CustomerName, (string?)null)
            .SetProperty(order => order.CustomerEmail, (string?)null)
            .SetProperty(order => order.CustomerPhone, (string?)null)
            .SetProperty(order => order.ShippingAddress, (string?)null)
            .SetProperty(order => order.ZipCode, (string?)null), ct);

        request.Status = "Completed";
        request.ProcessedAt = now;
        request.BlockingReason = null;
    }

    private static string BlockerDescription(IEnumerable<AccountDeletionBlocker> blockers) => string.Join(", ", blockers.Select(blocker => $"#{blocker.OrderId.ToString()[..8].ToUpperInvariant()} ({blocker.Status})"));
}
