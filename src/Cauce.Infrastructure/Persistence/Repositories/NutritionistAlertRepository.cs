using Cauce.Application.Common.Interfaces.Notifications;
using Cauce.Application.Notifications.Dtos;
using Cauce.Domain.Notifications;
using Cauce.Domain.Notifications.Enums;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementación de <see cref="INutritionistAlertRepository"/> sobre <see cref="CauceDbContext"/>.
/// </summary>
public sealed class NutritionistAlertRepository : INutritionistAlertRepository
{
    private readonly CauceDbContext _context;

    /// <summary>
    /// Inicializa el repositorio con el contexto de base de datos.
    /// </summary>
    public NutritionistAlertRepository(CauceDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(
        Guid nutritionistId, Guid patientId, NutritionistAlertType type, DateTime occurredAt, CancellationToken ct = default)
    {
        // También mira los avisos agregados en esta unidad de trabajo que aún no se guardan.
        if (_context.NutritionistAlerts.Local.Any(alert => alert.NutritionistId == nutritionistId
            && alert.PatientId == patientId && alert.Type == type && alert.OccurredAt == occurredAt))
        {
            return true;
        }

        return await _context.NutritionistAlerts
            .AsNoTracking()
            .AnyAsync(alert => alert.NutritionistId == nutritionistId
                && alert.PatientId == patientId
                && alert.Type == type
                && alert.OccurredAt == occurredAt, ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task AddAsync(NutritionistAlert alert, CancellationToken ct = default)
    {
        await _context.NutritionistAlerts.AddAsync(alert, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<NutritionistAlert?> FindOwnedAsync(Guid alertId, Guid nutritionistId, CancellationToken ct = default)
    {
        return _context.NutritionistAlerts
            .FirstOrDefaultAsync(alert => alert.Id == alertId && alert.NutritionistId == nutritionistId, ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<NutritionistAlert>> ListUnreadAsync(Guid nutritionistId, CancellationToken ct = default)
    {
        return await _context.NutritionistAlerts
            .Where(alert => alert.NutritionistId == nutritionistId && alert.ReadAt == null)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<NutritionistAlertPage> ListPageAsync(Guid nutritionistId, int page, int pageSize, CancellationToken ct = default)
    {
        var own = _context.NutritionistAlerts.AsNoTracking().Where(alert => alert.NutritionistId == nutritionistId);

        var total = await own.CountAsync(ct).ConfigureAwait(false);
        var unread = await own.CountAsync(alert => alert.ReadAt == null, ct).ConfigureAwait(false);

        var rows = await (from alert in own
                          join patient in _context.Users.AsNoTracking() on alert.PatientId equals patient.Id
                          orderby alert.OccurredAt descending, alert.Id
                          select new NutritionistAlertRow(
                              alert.Id, alert.Type, alert.PatientId, patient.FullName, patient.PatientCode, alert.OccurredAt, alert.ReadAt))
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return new NutritionistAlertPage(rows, total, unread);
    }

    /// <inheritdoc />
    public async Task RemoveForPatientAsync(Guid patientId, CancellationToken ct = default)
    {
        var alerts = await _context.NutritionistAlerts
            .Where(alert => alert.PatientId == patientId)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        _context.NutritionistAlerts.RemoveRange(alerts);
    }
}
