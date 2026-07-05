using Cauce.Application.Common.Interfaces.Notifications;
using Cauce.Domain.Notifications;
using Cauce.Domain.Notifications.Enums;
using Cauce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Infrastructure.Notifications;

/// <summary>
/// Implementación de <see cref="INotificationScheduler"/> sobre <see cref="CauceDbContext"/>. Enrola
/// la notificación en el <c>ChangeTracker</c> (sin <c>SaveChanges</c>) y ofrece la verificación de
/// existencia que respalda el efecto exactly-once ante el re-procesamiento del outbox (DEC-B5-05).
/// </summary>
public sealed class NotificationScheduler : INotificationScheduler
{
    private readonly CauceDbContext _context;

    /// <summary>
    /// Inicializa el planificador con el contexto de base de datos.
    /// </summary>
    /// <param name="context">Contexto de Entity Framework Core.</param>
    public NotificationScheduler(CauceDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task ScheduleAsync(Notification notification, CancellationToken ct = default)
    {
        await _context.Notifications.AddAsync(notification, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<bool> ExistsForRelatedAsync(
        Guid userId,
        string relatedEntityType,
        Guid relatedEntityId,
        NotificationType type,
        CancellationToken ct = default)
    {
        return _context.Notifications
            .AsNoTracking()
            .AnyAsync(
                notification => notification.UserId == userId
                    && notification.Type == type
                    && notification.RelatedEntityType == relatedEntityType
                    && notification.RelatedEntityId == relatedEntityId,
                ct);
    }
}
