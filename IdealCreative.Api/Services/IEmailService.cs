namespace IdealCreative.Api.Services;

public interface IEmailService
{
    Task SendPasswordResetAsync(string recipientEmail, string resetUrl, CancellationToken cancellationToken = default);
    Task SendPasswordChangedAsync(string recipientEmail, CancellationToken cancellationToken = default);
}
