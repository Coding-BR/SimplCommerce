namespace IdealCreative.Api.Services;

public sealed class AccountDeletionCleanupService(IServiceScopeFactory scopes, ILogger<AccountDeletionCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<AccountDeletionService>();
                var completed = await service.FinalizePendingAsync(stoppingToken);
                if (completed > 0) logger.LogInformation("{Count} solicitação(ões) de exclusão foram concluídas.", completed);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { logger.LogError(exception, "Falha ao processar solicitações de exclusão de conta."); }

            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }
}
