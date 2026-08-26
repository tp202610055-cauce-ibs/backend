using Cauce.Application.Common.Interfaces.Identity;
using Microsoft.Extensions.Logging;

namespace Cauce.Infrastructure.Email;

/// <summary>
/// Implementación de <see cref="IEmailSender"/> basada en MailKit (vía <see cref="SmtpMessageDispatcher"/>).
/// En desarrollo apunta a Mailpit; en producción, al servidor SMTP del hospital. No registra el
/// contenido del correo ni datos personales; en particular, nunca registra la contraseña del reporte.
/// </summary>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly SmtpMessageDispatcher _dispatcher;
    private readonly EmailOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    /// <summary>
    /// Inicializa el remitente con el despachador SMTP y su configuración.
    /// </summary>
    /// <param name="dispatcher">Despachador SMTP compartido.</param>
    /// <param name="options">Opciones de correo.</param>
    /// <param name="logger">Logger de la categoría del remitente.</param>
    public SmtpEmailSender(
        SmtpMessageDispatcher dispatcher,
        Microsoft.Extensions.Options.IOptions<EmailOptions> options,
        ILogger<SmtpEmailSender> logger)
    {
        _dispatcher = dispatcher;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task SendNutritionistTemporaryCredentialsAsync(
        string recipientEmail,
        string fullName,
        string temporaryPassword,
        CancellationToken ct = default)
    {
        var textBody = EmailTemplates.BuildNutritionistCredentialsText(
            fullName, recipientEmail, temporaryPassword, _options.PortalAppBaseUrl);
        var htmlBody = EmailTemplates.BuildNutritionistCredentialsHtml(
            fullName, recipientEmail, temporaryPassword, _options.PortalAppBaseUrl);

        await _dispatcher
            .SendAsync(recipientEmail, fullName, EmailTemplates.NutritionistCredentialsSubject, textBody, htmlBody, ct)
            .ConfigureAwait(false);
        _logger.LogInformation("Transactional email '{Subject}' sent.", EmailTemplates.NutritionistCredentialsSubject);
    }

    /// <inheritdoc />
    public async Task SendPasswordResetLinkAsync(
        string recipientEmail,
        string fullName,
        string resetLink,
        CancellationToken ct = default)
    {
        var textBody = EmailTemplates.BuildPasswordResetText(fullName, resetLink);
        var htmlBody = EmailTemplates.BuildPasswordResetHtml(fullName, resetLink);

        await _dispatcher
            .SendAsync(recipientEmail, fullName, EmailTemplates.PasswordResetSubject, textBody, htmlBody, ct)
            .ConfigureAwait(false);
        _logger.LogInformation("Transactional email '{Subject}' sent.", EmailTemplates.PasswordResetSubject);
    }

    /// <inheritdoc />
    public async Task SendReportReadyAsync(
        string recipientEmail,
        string fullName,
        string presignedUrl,
        DateTime expiresAtUtc,
        CancellationToken ct = default)
    {
        var textBody = EmailTemplates.BuildReportReadyText(fullName, presignedUrl, expiresAtUtc);
        var htmlBody = EmailTemplates.BuildReportReadyHtml(fullName, presignedUrl, expiresAtUtc);

        await _dispatcher
            .SendAsync(recipientEmail, fullName, EmailTemplates.ReportReadySubject, textBody, htmlBody, ct)
            .ConfigureAwait(false);
        _logger.LogInformation("Transactional email '{Subject}' sent.", EmailTemplates.ReportReadySubject);
    }

    /// <inheritdoc />
    public async Task SendReportPasswordAsync(
        string recipientEmail,
        string fullName,
        string password,
        CancellationToken ct = default)
    {
        var textBody = EmailTemplates.BuildReportPasswordText(fullName, password);
        var htmlBody = EmailTemplates.BuildReportPasswordHtml(fullName, password);

        await _dispatcher
            .SendAsync(recipientEmail, fullName, EmailTemplates.ReportPasswordSubject, textBody, htmlBody, ct)
            .ConfigureAwait(false);
        // No se registra la contraseña (DEC-B5-11).
        _logger.LogInformation("Transactional email '{Subject}' sent.", EmailTemplates.ReportPasswordSubject);
    }

    /// <inheritdoc />
    public async Task SendAccountDeletionConfirmationAsync(
        string recipientEmail,
        string fullName,
        CancellationToken ct = default)
    {
        var textBody = EmailTemplates.BuildAccountDeletionText(fullName);
        var htmlBody = EmailTemplates.BuildAccountDeletionHtml(fullName);

        await _dispatcher
            .SendAsync(recipientEmail, fullName, EmailTemplates.AccountDeletionSubject, textBody, htmlBody, ct)
            .ConfigureAwait(false);
        _logger.LogInformation("Transactional email '{Subject}' sent.", EmailTemplates.AccountDeletionSubject);
    }
}
