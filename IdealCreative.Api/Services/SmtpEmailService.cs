using System.Text.Encodings.Web;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace IdealCreative.Api.Services;

public sealed class SmtpEmailService(IntegrationSettingsStore settingsStore, ILogger<SmtpEmailService> logger) : IEmailService
{
    public async Task SendPasswordResetAsync(string recipientEmail, string resetUrl, CancellationToken cancellationToken = default)
    {
        var settings = (await settingsStore.GetRuntimeAsync(cancellationToken)).Smtp;
        EnsureConfigured(settings);

        var escapedUrl = HtmlEncoder.Default.Encode(resetUrl);
        var body = $"""
            <!doctype html>
            <html lang="pt-BR">
            <body style="margin:0;background:#f5f7f4;font-family:Arial,sans-serif;color:#17251f">
              <main style="max-width:560px;margin:32px auto;padding:32px;background:#ffffff;border-radius:18px">
                <h1 style="margin:0 0 16px;color:#176b4d;font-size:24px">Redefinição de senha</h1>
                <p>Recebemos uma solicitação para redefinir a senha da sua conta IdealCreative.</p>
                <p style="margin:28px 0"><a href="{escapedUrl}" style="display:inline-block;padding:13px 20px;border-radius:10px;background:#176b4d;color:#ffffff;text-decoration:none;font-weight:bold">Criar nova senha</a></p>
                <p>Este link expira em 1 hora e pode ser usado uma única vez.</p>
                <p>Se você não solicitou essa alteração, ignore este e-mail. Sua senha atual continuará válida.</p>
              </main>
            </body>
            </html>
            """;

        await SendAsync(recipientEmail, "Redefina sua senha | IdealCreative", body, settings, cancellationToken);
    }

    public async Task SendPasswordChangedAsync(string recipientEmail, CancellationToken cancellationToken = default)
    {
        var settings = (await settingsStore.GetRuntimeAsync(cancellationToken)).Smtp;
        EnsureConfigured(settings);

        var body = """
            <!doctype html>
            <html lang="pt-BR">
            <body style="margin:0;background:#f5f7f4;font-family:Arial,sans-serif;color:#17251f">
              <main style="max-width:560px;margin:32px auto;padding:32px;background:#fff;border-radius:18px">
                <h1 style="margin:0 0 16px;color:#176b4d;font-size:24px">Senha alterada</h1>
                <p>A senha da sua conta IdealCreative foi redefinida.</p>
                <p>Se você não realizou essa alteração, entre em contato com o suporte imediatamente.</p>
              </main>
            </body>
            </html>
            """;

        await SendAsync(recipientEmail, "Sua senha foi alterada | IdealCreative", body, settings, cancellationToken);
    }

    // ─── Private helpers ──────────────────────────────────────────────────────

    private async Task SendAsync(
        string recipientEmail,
        string subject,
        string htmlBody,
        SmtpRuntimeSettings settings,
        CancellationToken cancellationToken)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(settings.FromName, settings.FromEmail));
        message.To.Add(MailboxAddress.Parse(recipientEmail));
        message.Subject = subject;

        var builder = new BodyBuilder { HtmlBody = htmlBody };
        message.Body = builder.ToMessageBody();

        logger.LogInformation("Enviando e-mail '{Subject}' via SMTP {Host}:{Port}", subject, settings.Host, settings.Port);

        using var client = new SmtpClient();

        // SecureSocketOptions.Auto:
        //   - porta 465 → SSL implícito
        //   - porta 587 → STARTTLS (obrigatório para Brevo)
        //   - porta 25  → tenta STARTTLS se disponível
        var socketOptions = settings.UseSsl
            ? SecureSocketOptions.SslOnConnect   // porta 465
            : SecureSocketOptions.StartTls;       // porta 587 (Brevo/STARTTLS)

        await client.ConnectAsync(settings.Host, settings.Port, socketOptions, cancellationToken);

        if (!string.IsNullOrWhiteSpace(settings.Username))
            await client.AuthenticateAsync(settings.Username, settings.Password, cancellationToken);

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);

        logger.LogInformation("E-mail '{Subject}' enviado com sucesso para {Recipient}", subject, recipientEmail);
    }

    private static void EnsureConfigured(SmtpRuntimeSettings settings)
    {
        if (!settings.Enabled || string.IsNullOrWhiteSpace(settings.Host) || string.IsNullOrWhiteSpace(settings.FromEmail))
            throw new InvalidOperationException("O SMTP não está habilitado e configurado no painel administrativo.");
    }
}
