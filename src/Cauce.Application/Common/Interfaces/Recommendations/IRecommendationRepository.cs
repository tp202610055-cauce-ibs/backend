using Cauce.Application.Common.Models;
using Cauce.Domain.Recommendations;
using Cauce.Domain.Recommendations.Enums;

namespace Cauce.Application.Common.Interfaces.Recommendations;

/// <summary>
/// Repositorio del agregado <see cref="Recommendation"/>.
/// </summary>
public interface IRecommendationRepository
{
    /// <summary>
    /// Busca una recomendación por su identificador, con seguimiento de cambios para permitir
    /// mutaciones de estado.
    /// </summary>
    /// <param name="recommendationId">Identificador de la recomendación.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>La recomendación o <see langword="null"/> si no existe.</returns>
    Task<Recommendation?> GetByIdAsync(Guid recommendationId, CancellationToken ct = default);

    /// <summary>
    /// Busca una recomendación por su identificador incluyendo sus ítems y su retroalimentación,
    /// con seguimiento de cambios para permitir la transición de expiración on-read.
    /// </summary>
    /// <param name="recommendationId">Identificador de la recomendación.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>La recomendación con sus detalles o <see langword="null"/> si no existe.</returns>
    Task<Recommendation?> GetByIdWithDetailsAsync(Guid recommendationId, CancellationToken ct = default);

    /// <summary>
    /// Lista paginadas las recomendaciones de un paciente, opcionalmente filtradas por estado,
    /// ordenadas por fecha de generación descendente, con seguimiento de cambios para permitir
    /// la transición de expiración on-read.
    /// </summary>
    /// <param name="patientId">Identificador del paciente.</param>
    /// <param name="status">Estado por el que filtrar, o <see langword="null"/> para todos.</param>
    /// <param name="page">Número de página (base 1).</param>
    /// <param name="pageSize">Tamaño de página.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>La página de recomendaciones.</returns>
    Task<PagedResult<Recommendation>> ListByPatientAsync(
        Guid patientId,
        RecommendationStatus? status,
        int page,
        int pageSize,
        CancellationToken ct = default);

    /// <summary>
    /// Lista paginadas las recomendaciones en revisión pendiente de los pacientes asignados a un
    /// nutricionista, que aún no han expirado, ordenadas por fecha de generación ascendente. No
    /// transita las expiradas (responsabilidad del worker del Prompt 5; ver DEC-B4-06).
    /// </summary>
    /// <param name="nutritionistId">Identificador del nutricionista.</param>
    /// <param name="page">Número de página (base 1).</param>
    /// <param name="pageSize">Tamaño de página.</param>
    /// <param name="now">Marca de tiempo UTC de referencia para excluir expiradas.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>La página de recomendaciones pendientes de revisión.</returns>
    Task<PagedResult<Recommendation>> ListPendingReviewByNutritionistAsync(
        Guid nutritionistId,
        int page,
        int pageSize,
        DateTime now,
        CancellationToken ct = default);

    /// <summary>
    /// Agrega una nueva recomendación al contexto de persistencia.
    /// </summary>
    /// <param name="recommendation">Recomendación a agregar.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Tarea que representa la operación asíncrona.</returns>
    Task AddAsync(Recommendation recommendation, CancellationToken ct = default);

    /// <summary>
    /// Agrega una retroalimentación como entidad nueva al contexto de persistencia. Se usa al
    /// incorporar la retroalimentación a una recomendación ya persistida, para que Entity Framework
    /// la trate como inserción y no como actualización.
    /// </summary>
    /// <param name="feedback">Retroalimentación a agregar.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Tarea que representa la operación asíncrona.</returns>
    Task AddFeedbackAsync(RecommendationFeedback feedback, CancellationToken ct = default);
}
