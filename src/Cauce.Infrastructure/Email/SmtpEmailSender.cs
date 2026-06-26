using Cauce.Application.Common.Interfaces.Identity;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Cauce.Infrastructure.Email;

/// <summary>
/// Implementación de <see cref="IEmailSender"/> basada en MailKit. En desarrollo
/// apunta a Mailpit; en producción, al servidor SMTP del hospital. No registra el
/// contenido del correo ni datos personales.
/// </summary>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly EmailOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    /// <summary>
    /// Inicializa el remitente con su configuración.
    /// </summary>
    /// <param name="options">Opciones de correo.</param>
    /// <param name="logger">Logger de la categoría del remitente.</param>
    public SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task SendNutritionistTemporaryCredentialsAsync(
        string recipientEmail,
        string fullName,
        string temporaryPassword,
        CancellationToken ct = default)
    {
        var textBody = EmailTemplates.BuildNutritionistCredentialsText(
            fullName, recipientEmail, temporaryPassword, _options.AppBaseUrl);
        var htmlBody = EmailTemplates.BuildNutritionistCredentialsHtml(
            fullName, recipientEmail, temporaryPassword, _options.AppBaseUrl);

        return SendAsync(recipientEmail, fullName, EmailTemplates.NutritionistCredentialsSubject, textBody, htmlBody, ct);
    }

    /// <inheritdoc />
    public Task SendPasswordResetLinkAsync(
        string recipientEmail,
        string fullName,
        string resetLink,
        CancellationToken ct = default)
    {
        var textBody = EmailTemplates.BuildPasswordResetText(fullName, resetLink);
        var htmlBody = EmailTemplates.BuildPasswordResetHtml(fullName, resetLink);

        return SendAsync(recipientEmail, fullName, EmailTemplates.PasswordResetSubject, textBody, htmlBody, ct);
    }

    private async Task SendAsync(
        string recipientEmail,
        string recipientName,
        string subject,
        string textBody,
        string htmlBody,
        CancellationToken ct)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        message.To.Add(new MailboxAddress(recipientName, recipientEmail));
        message.Subject = subject;
        message.Body = new BodyBuilder
        {
            TextBody = textBody,
            HtmlBody = htmlBody
        }.ToMessageBody();

        using var client = new SmtpClient();
        var socketOptions = _options.UseSsl
            ? SecureSocketOptions.StartTlsWhenAvailable
            : SecureSocketOptions.None;

        await client.ConnectAsync(_options.SmtpHost, _options.SmtpPort, socketOptions, ct).ConfigureAwait(false);

        if (!string.IsNullOrEmpty(_options.Username))
        {
            await client.AuthenticateAsync(_options.Username, _options.Password ?? string.Empty, ct).ConfigureAwait(false);
        }

        await client.SendAsync(message, ct).ConfigureAwait(false);
        await client.DisconnectAsync(quit: true, ct).ConfigureAwait(false);

        _logger.LogInformation("Transactional email '{Subject}' sent successfully.", subject);
    }
}
