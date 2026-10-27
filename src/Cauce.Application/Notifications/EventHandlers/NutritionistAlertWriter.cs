using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Common.Interfaces.Notifications;
using Cauce.Domain.Identity.Enums;
using Cauce.Domain.Notifications;
using Cauce.Domain.Notifications.Enums;

namespace Cauce.Application.Notifications.EventHandlers;

/// <summary>
/// Escribe un aviso en la bandeja si el paciente sigue con cuenta y el aviso de ese hecho no existe aún.
/// </summary>
internal static class NutritionistAlertWriter
{
    public static async Task WriteAsync(
        IUserRepository userRepository,
        INutritionistAlertRepository alertRepository,
        Guid nutritionistId,
        Guid patientId,
        NutritionistAlertType type,
        DateTime occurredAt,
        CancellationToken ct)
    {
        // Un paciente que ya no existe o que dio de baja su cuenta no deja avisos.
        var patient = await userRepository.FindByIdAsync(patientId, ct).ConfigureAwait(false);
        if (patient is null || patient.Status == UserStatus.Inactive)
        {
            return;
        }

        var key = NutritionistAlert.TruncateToMicroseconds(occurredAt);
        var exists = await alertRepository.ExistsAsync(nutritionistId, patientId, type, key, ct).ConfigureAwait(false);
        if (exists)
        {
            return;
        }

        await alertRepository
            .AddAsync(NutritionistAlert.Record(nutritionistId, patientId, type, occurredAt), ct)
            .ConfigureAwait(false);
    }
}
