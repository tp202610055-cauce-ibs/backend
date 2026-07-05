using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Cauce.Infrastructure.Email;

/// <summary>
/// Despachador SMTP compartido basado en MailKit. Encapsula la conexión, autenticación y envío de un
/// mensaje, para reutilizarlo entre los distintos remitentes de correo sin duplicar la lógica de
/// transporte. No registra el contenido del correo ni datos personales.
/// </summary>
public sealed class SmtpMessageDispatcher
{
    private readonly EmailOptions _options;

    /// <summary>
    /// Inicializa el despachador con la configuración de correo.
    /// </summary>
    /// <param name="options">Opciones de correo.</param>
    public SmtpMessageDispatcher(IOptions<EmailOptions> options)
    {
        _options = options.Value;
    }

    /// <summary>
    /// Envía un mensaje de texto y HTML al destinatario indicado.
    /// </summary>
    /// <param name="recipientEmail">Correo del destinatario.</param>
    /// <param name="recipientName">Nombre del destinatario.</param>
    /// <param name="subject">Asunto.</param>
    /// <param name="textBody">Cuerpo en texto plano.</param>
    /// <param name="htmlBody">Cuerpo en HTML.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Tarea que representa la operación asíncrona.</returns>
    public async Task SendAsync(
        string recipientEmail,
        string recipientName,
        string subject,
        string textBody,
        string htmlBody,
        CancellationToken ct = default)
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
    }
}
