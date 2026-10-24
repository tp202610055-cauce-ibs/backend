using System.Text.Json;
using Cauce.Application.Common.Interfaces;
using Cauce.Domain.Auditing.Enums;
using Cauce.Domain.Identity;
using Cauce.Domain.Patients.Enums;
using Cauce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Infrastructure.Qa.Identity;

/// <summary>
/// Retira un nutricionista con <see cref="User.RetireNutritionist"/> y desactiva sus vínculos, para que una cuenta
/// pendiente o ya activada pueda nacer de nuevo con el mismo correo.
/// </summary>
public sealed class QaNutritionistRetirer : IQaAccountRetirer
{
    private readonly CauceDbContext _db;
    private readonly IAuditLogger _auditLogger;

    /// <summary>
    /// Inicializa el retirador con el contexto y la bitácora.
    /// </summary>
    public QaNutritionistRetirer(CauceDbContext db, IAuditLogger auditLogger)
    {
        _db = db;
        _auditLogger = auditLogger;
    }

    /// <inheritdoc />
    public string RoleName => UserRoles.Nutritionist;

    /// <inheritdoc />
    public async Task RetireAsync(User user, DateTime utcNow, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(user);
        user.RetireNutritionist(user.RoleId);

        var links = await _db.NutritionistPatients
            .Where(link => link.NutritionistId == user.Id && link.Status == AssignmentStatus.Active)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        foreach (var link in links)
        {
            link.Deactivate(utcNow);
        }

        // users no tiene trigger: el retiro queda en la bitácora con una fila explícita.
        await _auditLogger.LogAsync(
            AuditActionType.Delete,
            nameof(User),
            user.Id,
            oldValuesHash: null,
            newValuesHash: null,
            additionalContext: JsonSerializer.Serialize(new { operation = "retirement", actor = "qa_command" }),
            cancellationToken: ct).ConfigureAwait(false);
    }
}
