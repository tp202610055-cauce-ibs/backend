using System.Collections.Concurrent;
using Cauce.Application.Common.Interfaces.Identity;

namespace Cauce.Api.IntegrationTests.Identity.Support;

/// <summary>
/// Implementación en memoria de <see cref="IEmailSender"/> para pruebas de
/// integración. Registra los correos enviados en lugar de contactar un servidor SMTP.
/// </summary>
public sealed class FakeEmailSender : IEmailSender
{
    /// <summary>
    /// Representa un correo enviado.
    /// </summary>
    /// <param name="Kind">
    /// Tipo de correo (<c>password-reset</c>, <c>report-ready</c>, <c>report-password</c> o
    /// <c>account-deletion</c>).
    /// </param>
    /// <param name="Recipient">Destinatario.</param>
    /// <param name="Payload">Contenido relevante (enlace, contraseña del reporte o nombre).</param>
    public sealed record SentEmail(string Kind, string Recipient, string Payload);

    /// <summary>
    /// Correos enviados durante la prueba.
    /// </summary>
    public ConcurrentBag<SentEmail> SentEmails { get; } = [];

    /// <inheritdoc />
    public Task SendPasswordResetLinkAsync(string recipientEmail, string fullName, string resetLink, CancellationToken ct = default)
    {
        SentEmails.Add(new SentEmail("password-reset", recipientEmail, resetLink));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SendReportReadyAsync(string recipientEmail, string fullName, string presignedUrl, DateTime expiresAtUtc, CancellationToken ct = default)
    {
        SentEmails.Add(new SentEmail("report-ready", recipientEmail, presignedUrl));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SendReportPasswordAsync(string recipientEmail, string fullName, string password, CancellationToken ct = default)
    {
        SentEmails.Add(new SentEmail("report-password", recipientEmail, password));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task SendAccountDeletionConfirmationAsync(string recipientEmail, string fullName, CancellationToken ct = default)
    {
        SentEmails.Add(new SentEmail("account-deletion", recipientEmail, fullName));
        return Task.CompletedTask;
    }
}
