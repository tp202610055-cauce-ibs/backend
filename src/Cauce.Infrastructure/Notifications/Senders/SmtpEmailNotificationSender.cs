using Cauce.Application.Common.Interfaces.Notifications;
using Cauce.Domain.Notifications;
using Cauce.Domain.Notifications.Enums;
using Cauce.Infrastructure.Email;
using Cauce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cauce.Infrastructure.Notifications.Senders;

/// <summary>
/// Remitente de notificaciones por correo electrónico. Resuelve el correo del destinatario a partir
/// de su identificador y envía el título como asunto y el cuerpo como contenido.
/// </summary>
public sealed class SmtpEmailNotificationSender : INotificationSender
{
    private readonly CauceDbContext _context;
    private readonly SmtpMessageDispatcher _dispatcher;
    private readonly ILogger<SmtpEmailNotificationSender> _logger;

    /// <summary>
    /// Inicializa el remitente con sus dependencias.
    /// </summary>
    /// <param name="context">Contexto de base de datos, para resolver el correo del destinatario.</param>
    /// <param name="dispatcher">Despachador SMTP compartido.</param>
    /// <param name="logger">Logger de la categoría del remitente.</param>
    public SmtpEmailNotificationSender(
        CauceDbContext context,
        SmtpMessageDispatcher dispatcher,
        ILogger<SmtpEmailNotificationSender> logger)
    {
        _context = context;
        _dispatcher = dispatcher;
        _logger = logger;
    }

    /// <inheritdoc />
    public NotificationChannel Channel => NotificationChannel.Email;

    /// <inheritdoc />
    public async Task<NotificationSendResult> SendAsync(Notification notification, CancellationToken ct = default)
    {
        var recipient = await _context.Users
            .AsNoTracking()
            .Where(user => user.Id == notification.UserId)
            .Select(user => new { user.Email, user.FullName })
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (recipient is null)
        {
            return new NotificationSendResult(false, null, "El destinatario no tiene una cuenta local.");
        }

        try
        {
            await _dispatcher
                .SendAsync(recipient.Email, recipient.FullName, notification.Title, notification.Body, WrapHtml(notification.Body), ct)
                .ConfigureAwait(false);
            return new NotificationSendResult(true, null, null);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to send email notification {NotificationId}.", notification.Id);
            return new NotificationSendResult(false, null, exception.Message);
        }
    }

    private static string WrapHtml(string body)
    {
        return $"<p>{System.Net.WebUtility.HtmlEncode(body)}</p>";
    }
}
