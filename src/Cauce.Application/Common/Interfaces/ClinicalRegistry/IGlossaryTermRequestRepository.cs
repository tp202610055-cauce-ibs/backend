using Cauce.Domain.ClinicalRegistry;

namespace Cauce.Application.Common.Interfaces.ClinicalRegistry;

/// <summary>
/// Repositorio de las solicitudes de términos nuevos del glosario.
/// </summary>
public interface IGlossaryTermRequestRepository
{
    /// <summary>
    /// Devuelve la solicitud pendiente del nutricionista para el mismo término, sin distinguir mayúsculas ni tildes.
    /// </summary>
    Task<GlossaryTermRequest?> FindPendingAsync(Guid nutritionistId, string term, CancellationToken ct = default);

    /// <summary>
    /// Agrega una solicitud; se persiste con la unidad de trabajo.
    /// </summary>
    Task AddAsync(GlossaryTermRequest request, CancellationToken ct = default);
}
