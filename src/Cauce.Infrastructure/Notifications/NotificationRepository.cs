using Cauce.Application.Common.Interfaces.Notifications;
using Cauce.Domain.Notifications;
using Cauce.Domain.Notifications.Enums;
using Cauce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Infrastructure.Notifications;

/// <summary>
/// Implementación de <see cref="INotificationRepository"/> sobre <see cref="CauceDbContext"/>.
/// </summary>
public sealed class NotificationRepository : INotificationRepository
{
    private readonly CauceDbContext _context;

    /// <summary>
    /// Inicializa el repositorio con el contexto de base de datos.
    /// </summary>
    /// <param name="context">Contexto de Entity Framework Core.</param>
    public NotificationRepository(CauceDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Notification>> ListDispatchableAsync(DateTime now, int batchSize, CancellationToken ct = default)
    {
        // Con seguimiento de cambios (el despachador muta el estado). Se ordena por scheduled_for para
        // preservar el orden de envío (por ejemplo, URL antes que contraseña del reporte, ajuste 4).
        return await _context.Notifications
            .Where(notification => notification.Status == NotificationStatus.Pending && notification.ScheduledFor <= now)
            .OrderBy(notification => notification.ScheduledFor)
            .ThenBy(notification => notification.Id)
            .Take(batchSize)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task AddAsync(Notification notification, CancellationToken ct = default)
    {
        await _context.Notifications.AddAsync(notification, ct).ConfigureAwait(false);
    }
}
