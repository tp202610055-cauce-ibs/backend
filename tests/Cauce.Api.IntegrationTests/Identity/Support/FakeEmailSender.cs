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
    /// <param name="Kind">Tipo de correo (<c>credentials</c> o <c>password-reset</c>).</param>
    /// <param name="Recipient">Destinatario.</param>
    /// <param name="Payload">Contenido relevante (contraseña temporal o enlace).</param>
    public sealed record SentEmail(string Kind, string Recipient, string Payload);

    /// <summary>
    /// Correos enviados durante la prueba.
    /// </summary>
    public ConcurrentBag<SentEmail> SentEmails { get; } = [];

    /// <inheritdoc />
    public Task SendNutritionistTemporaryCredentialsAsync(string recipientEmail, string fullName, string temporaryPassword, CancellationToken ct = default)
    {
        SentEmails.Add(new SentEmail("credentials", recipientEmail, temporaryPassword));
        return Task.CompletedTask;
    }

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
}
