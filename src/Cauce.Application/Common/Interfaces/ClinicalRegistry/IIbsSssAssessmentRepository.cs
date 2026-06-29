using Cauce.Domain.ClinicalRegistry;

namespace Cauce.Application.Common.Interfaces.ClinicalRegistry;

/// <summary>
/// Repositorio del agregado <see cref="IbsSssAssessment"/> (evaluaciones IBS-SSS).
/// </summary>
public interface IIbsSssAssessmentRepository
{
    /// <summary>
    /// Busca una evaluación por su identificador.
    /// </summary>
    /// <param name="assessmentId">Identificador de la evaluación.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>La evaluación o <see langword="null"/> si no existe.</returns>
    Task<IbsSssAssessment?> FindByIdAsync(Guid assessmentId, CancellationToken ct = default);

    /// <summary>
    /// Busca la evaluación de línea base del paciente.
    /// </summary>
    /// <param name="patientId">Identificador del paciente.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>La evaluación de línea base, o <see langword="null"/> si no existe.</returns>
    Task<IbsSssAssessment?> FindBaselineByPatientAsync(Guid patientId, CancellationToken ct = default);

    /// <summary>
    /// Busca la evaluación más reciente del paciente.
    /// </summary>
    /// <param name="patientId">Identificador del paciente.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>La evaluación más reciente, o <see langword="null"/> si no existe.</returns>
    Task<IbsSssAssessment?> FindLatestByPatientAsync(Guid patientId, CancellationToken ct = default);

    /// <summary>
    /// Lista todas las evaluaciones del paciente, ordenadas por número de ciclo ascendente.
    /// </summary>
    /// <param name="patientId">Identificador del paciente.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Las evaluaciones del paciente.</returns>
    Task<IReadOnlyList<IbsSssAssessment>> ListByPatientAsync(Guid patientId, CancellationToken ct = default);

    /// <summary>
    /// Obtiene el siguiente número de ciclo para una evaluación periódica del paciente.
    /// </summary>
    /// <param name="patientId">Identificador del paciente.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>El siguiente número de ciclo (≥1).</returns>
    Task<int> GetNextCycleNumberAsync(Guid patientId, CancellationToken ct = default);

    /// <summary>
    /// Agrega una nueva evaluación al contexto de persistencia.
    /// </summary>
    /// <param name="assessment">Evaluación a agregar.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Tarea que representa la operación asíncrona.</returns>
    Task AddAsync(IbsSssAssessment assessment, CancellationToken ct = default);
}
