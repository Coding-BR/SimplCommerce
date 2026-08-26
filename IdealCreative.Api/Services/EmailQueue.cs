using System.Threading.Channels;

namespace IdealCreative.Api.Services;

public enum EmailWorkType { PasswordReset, PasswordChanged }
public sealed record EmailWorkItem(EmailWorkType Type, string RecipientEmail, string? ResetUrl = null);

public interface IEmailQueue
{
    ValueTask QueueAsync(EmailWorkItem item, CancellationToken cancellationToken = default);
    IAsyncEnumerable<EmailWorkItem> ReadAllAsync(CancellationToken cancellationToken);
}

public sealed class EmailQueue : IEmailQueue
{
    private readonly Channel<EmailWorkItem> channel = Channel.CreateBounded<EmailWorkItem>(new BoundedChannelOptions(100)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleReader = true,
        SingleWriter = false
    });

    public ValueTask QueueAsync(EmailWorkItem item, CancellationToken cancellationToken = default) => channel.Writer.WriteAsync(item, cancellationToken);
    public IAsyncEnumerable<EmailWorkItem> ReadAllAsync(CancellationToken cancellationToken) => channel.Reader.ReadAllAsync(cancellationToken);
}

public sealed class EmailQueueWorker(IEmailQueue queue, IServiceScopeFactory scopes, ILogger<EmailQueueWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var item in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var email = scope.ServiceProvider.GetRequiredService<IEmailService>();
                if (item.Type == EmailWorkType.PasswordReset && !string.IsNullOrWhiteSpace(item.ResetUrl))
                    await email.SendPasswordResetAsync(item.RecipientEmail, item.ResetUrl, stoppingToken);
                else if (item.Type == EmailWorkType.PasswordChanged)
                    await email.SendPasswordChangedAsync(item.RecipientEmail, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception exception)
            {
                logger.LogError(exception, "Falha ao processar e-mail transacional do tipo {EmailWorkType}", item.Type);
            }
        }
    }
}
