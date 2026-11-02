using Cauce.Application.Notifications.Dtos;
using Cauce.Domain.Notifications;
using Cauce.Domain.Notifications.Enums;

namespace Cauce.Application.Common.Interfaces.Notifications;

/// <summary>
/// Repositorio de la bandeja de avisos del nutricionista.
/// </summary>
public interface INutritionistAlertRepository
{
    /// <summary>Indica si ya existe el aviso de ese hecho, para que los handlers sean idempotentes.</summary>
    Task<bool> ExistsAsync(Guid nutritionistId, Guid patientId, NutritionistAlertType type, DateTime occurredAt, CancellationToken ct = default);

    /// <summary>Agrega un aviso; se persiste con la unidad de trabajo.</summary>
    Task AddAsync(NutritionistAlert alert, CancellationToken ct = default);

    /// <summary>Devuelve el aviso si pertenece al nutricionista, con seguimiento de cambios.</summary>
    Task<NutritionistAlert?> FindOwnedAsync(Guid alertId, Guid nutritionistId, CancellationToken ct = default);

    /// <summary>Devuelve los avisos sin leer del nutricionista, con seguimiento de cambios.</summary>
    Task<IReadOnlyList<NutritionistAlert>> ListUnreadAsync(Guid nutritionistId, CancellationToken ct = default);

    /// <summary>Página de avisos del nutricionista, del más reciente al más antiguo, con el nombre del paciente.</summary>
    Task<NutritionistAlertPage> ListPageAsync(Guid nutritionistId, int page, int pageSize, CancellationToken ct = default);

    /// <summary>Marca para borrar todos los avisos que mencionan al paciente; se persiste con la unidad de trabajo.</summary>
    Task RemoveForPatientAsync(Guid patientId, CancellationToken ct = default);
}
