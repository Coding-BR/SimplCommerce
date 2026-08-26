using System.Text.Json;
using IdealCreative.Api.Controllers;
using IdealCreative.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace IdealCreative.Api.Services;

public sealed class OrderReservationCleanupService(IServiceScopeFactory scopes, ILogger<OrderReservationCleanupService> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await ReleaseExpiredAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogError(exception, "Falha ao liberar reservas de estoque expiradas."); }
            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }

    private async Task ReleaseExpiredAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cutoff = DateTimeOffset.UtcNow.AddHours(-2);
        var expired = await db.Orders.AsNoTracking().Where(order => (order.Status == "Pending" || order.Status == "AwaitingPayment") && order.CreatedAt < cutoff).OrderBy(order => order.CreatedAt).Select(order => new { order.Id, order.ItemsJson }).Take(100).ToListAsync(ct);
        foreach (var order in expired)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var claimed = await db.Orders.Where(row => row.Id == order.Id && (row.Status == "Pending" || row.Status == "AwaitingPayment") && row.CreatedAt < cutoff).ExecuteUpdateAsync(update => update.SetProperty(row => row.Status, "Cancelled").SetProperty(row => row.PaymentFailureReason, "Reserva expirada sem confirmação de pagamento."), ct);
            if (claimed == 1)
            {
                var items = JsonSerializer.Deserialize<List<CommerceController.StoredCartItem>>(order.ItemsJson, JsonOptions) ?? [];
                foreach (var item in items.Where(item => !item.IsDigital && Guid.TryParse(item.ProductId, out _)))
                {
                    var productId = Guid.Parse(item.ProductId);
                    await db.Products.Where(product => product.Id == productId).ExecuteUpdateAsync(update => update.SetProperty(product => product.Stock, product => product.Stock + item.Quantity), ct);
                }
                await transaction.CommitAsync(ct);
                logger.LogInformation("Reserva expirada do pedido {OrderId} foi liberada.", order.Id);
            }
            else await transaction.RollbackAsync(ct);
        }
    }
}
