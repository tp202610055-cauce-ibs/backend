using Cauce.Domain.ClinicalRegistry;

namespace Cauce.Application.Common.Interfaces.ClinicalRegistry;

/// <summary>
/// Repositorio de las agendas de evaluaciones IBS-SSS (US12 CA02).
/// </summary>
public interface IIbsSssAssessmentScheduleRepository
{
    /// <summary>
    /// Busca la agenda abierta (ni completada ni perdida) más reciente del paciente. Se devuelve
    /// rastreada para poder marcarla como completada.
    /// </summary>
    /// <param name="patientId">Identificador del paciente.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>La agenda abierta, o <see langword="null"/> si no hay ninguna.</returns>
    Task<IbsSssAssessmentSchedule?> FindOpenByPatientAsync(Guid patientId, CancellationToken ct = default);

    /// <summary>
    /// Agrega una nueva agenda al contexto de persistencia.
    /// </summary>
    /// <param name="schedule">Agenda a agregar.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Tarea que representa la operación asíncrona.</returns>
    Task AddAsync(IbsSssAssessmentSchedule schedule, CancellationToken ct = default);
}
