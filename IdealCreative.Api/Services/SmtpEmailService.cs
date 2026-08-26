using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.Encodings.Web;

namespace IdealCreative.Api.Services;

public sealed class SmtpEmailService(IntegrationSettingsStore settingsStore, ILogger<SmtpEmailService> logger) : IEmailService
{
    public async Task SendPasswordResetAsync(string recipientEmail, string resetUrl, CancellationToken cancellationToken = default)
    {
        var settings = (await settingsStore.GetRuntimeAsync(cancellationToken)).Smtp;
        EnsureConfigured(settings);

        var escapedUrl = HtmlEncoder.Default.Encode(resetUrl);
        using var message = new MailMessage
        {
            From = new MailAddress(settings.FromEmail, settings.FromName),
            Subject = "Redefina sua senha | IdealCreative",
            Body = $$"""
                <!doctype html>
                <html lang="pt-BR">
                <body style="margin:0;background:#f5f7f4;font-family:Arial,sans-serif;color:#17251f">
                  <main style="max-width:560px;margin:32px auto;padding:32px;background:#ffffff;border-radius:18px">
                    <h1 style="margin:0 0 16px;color:#176b4d;font-size:24px">Redefinição de senha</h1>
                    <p>Recebemos uma solicitação para redefinir a senha da sua conta IdealCreative.</p>
                    <p style="margin:28px 0"><a href="{{escapedUrl}}" style="display:inline-block;padding:13px 20px;border-radius:10px;background:#176b4d;color:#ffffff;text-decoration:none;font-weight:bold">Criar nova senha</a></p>
                    <p>Este link expira em 1 hora e pode ser usado uma única vez.</p>
                    <p>Se você não solicitou essa alteração, ignore este e-mail. Sua senha atual continuará válida.</p>
                  </main>
                </body>
                </html>
                """,
            IsBodyHtml = true,
            BodyEncoding = System.Text.Encoding.UTF8,
            SubjectEncoding = System.Text.Encoding.UTF8
        };
        message.To.Add(recipientEmail);

        using var client = CreateClient(settings);

        logger.LogInformation("Enviando e-mail de redefinição de senha via SMTP {Host}:{Port}", settings.Host, settings.Port);
        cancellationToken.ThrowIfCancellationRequested();
        await client.SendMailAsync(message);
    }

    public async Task SendPasswordChangedAsync(string recipientEmail, CancellationToken cancellationToken = default)
    {
        await SendAsync(
            recipientEmail,
            "Sua senha foi alterada | IdealCreative",
            "<h1 style=\"margin:0 0 16px;color:#176b4d;font-size:24px\">Senha alterada</h1><p>A senha da sua conta IdealCreative foi redefinida.</p><p>Se você não realizou essa alteração, entre em contato com o suporte imediatamente.</p>",
            cancellationToken);
    }

    private async Task SendAsync(string recipientEmail, string subject, string content, CancellationToken cancellationToken)
    {
        var settings = (await settingsStore.GetRuntimeAsync(cancellationToken)).Smtp;
        EnsureConfigured(settings);

        using var message = new MailMessage
        {
            From = new MailAddress(settings.FromEmail, settings.FromName),
            Subject = subject,
            Body = $"<!doctype html><html lang=\"pt-BR\"><body style=\"margin:0;background:#f5f7f4;font-family:Arial,sans-serif;color:#17251f\"><main style=\"max-width:560px;margin:32px auto;padding:32px;background:#fff;border-radius:18px\">{content}</main></body></html>",
            IsBodyHtml = true,
            BodyEncoding = Encoding.UTF8,
            SubjectEncoding = Encoding.UTF8
        };
        message.To.Add(recipientEmail);
        using var client = CreateClient(settings);
        cancellationToken.ThrowIfCancellationRequested();
        await client.SendMailAsync(message);
    }

    private static void EnsureConfigured(SmtpRuntimeSettings settings)
    {
        if (!settings.Enabled || string.IsNullOrWhiteSpace(settings.Host) || string.IsNullOrWhiteSpace(settings.FromEmail))
            throw new InvalidOperationException("O SMTP não está habilitado e configurado no painel administrativo.");
    }

    private static SmtpClient CreateClient(SmtpRuntimeSettings settings)
    {
        var client = new SmtpClient(settings.Host, settings.Port)
        {
            EnableSsl = settings.UseSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Timeout = settings.TimeoutMilliseconds
        };
        if (!string.IsNullOrWhiteSpace(settings.Username))
            client.Credentials = new NetworkCredential(settings.Username, settings.Password);
        return client;
    }
}
