using System.Text.Json;
using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Common.Interfaces.Patients;
using Cauce.Domain.Auditing.Enums;
using Cauce.Domain.Identity;
using Cauce.Domain.Patients.Exceptions;

namespace Cauce.Application.Patients.Services;

/// <summary>
/// Reglas comunes de las lecturas clínicas del nutricionista sobre un paciente: exigen una asignación
/// activa (403 si no) y dejan rastro en la bitácora como <see cref="AuditActionType.ViewPatientRecord"/>.
/// </summary>
public static class NutritionistPatientRecordAccess
{
    /// <summary>
    /// Verifica que el usuario autenticado sea un nutricionista con asignación activa sobre el paciente.
    /// </summary>
    /// <returns>El identificador local del nutricionista.</returns>
    /// <exception cref="PatientAccessNotAuthorizedException">Si no hay asignación activa.</exception>
    public static async Task<Guid> EnsureActiveAssignmentAsync(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        INutritionistPatientRepository nutritionistPatientRepository,
        Guid patientId,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(currentUserService);
        ArgumentNullException.ThrowIfNull(userRepository);
        ArgumentNullException.ThrowIfNull(nutritionistPatientRepository);

        var keycloakId = currentUserService.UserId
            ?? throw new UnauthorizedAccessException("No hay un usuario autenticado en la petición.");

        var nutritionist = await userRepository.FindByKeycloakIdAsync(keycloakId.ToString(), ct).ConfigureAwait(false)
            ?? throw new UnauthorizedAccessException("El usuario autenticado no tiene una cuenta local asociada.");

        var nutritionistRoleId = await userRepository.GetRoleIdAsync(UserRoles.Nutritionist, ct).ConfigureAwait(false);
        if (nutritionist.RoleId != nutritionistRoleId)
        {
            throw new UnauthorizedAccessException("Solo un nutricionista puede consultar los registros de un paciente.");
        }

        var hasAccess = await nutritionistPatientRepository
            .ActiveAssignmentExistsAsync(nutritionist.Id, patientId, ct)
            .ConfigureAwait(false);
        if (!hasAccess)
        {
            throw new PatientAccessNotAuthorizedException();
        }

        return nutritionist.Id;
    }

    /// <summary>
    /// Registra en la bitácora la lectura y la persiste. <paramref name="view"/> nombra la vista consultada.
    /// </summary>
    public static async Task RecordViewAsync(
        IAuditLogger auditLogger,
        IUnitOfWork unitOfWork,
        Guid patientId,
        string view,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(auditLogger);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        await auditLogger.LogAsync(
            AuditActionType.ViewPatientRecord, nameof(User), patientId,
            oldValuesHash: null, newValuesHash: null,
            additionalContext: JsonSerializer.Serialize(new { view }),
            cancellationToken: ct).ConfigureAwait(false);

        await unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
