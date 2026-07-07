using Cauce.Application.Patients.Dtos;
using Cauce.Domain.Patients;

namespace Cauce.Application.Common.Interfaces.Patients;

/// <summary>
/// Repositorio del agregado <see cref="NutritionistPatient"/>.
/// </summary>
public interface INutritionistPatientRepository
{
    /// <summary>
    /// Busca la asignación activa de un paciente.
    /// </summary>
    /// <param name="patientId">Identificador del paciente.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>La asignación activa o <see langword="null"/> si no la hay.</returns>
    Task<NutritionistPatient?> FindActiveByPatientAsync(Guid patientId, CancellationToken ct = default);

    /// <summary>
    /// Lista las asignaciones activas de un nutricionista.
    /// </summary>
    /// <param name="nutritionistId">Identificador del nutricionista.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Lista de asignaciones activas.</returns>
    Task<IReadOnlyList<NutritionistPatient>> ListActiveByNutritionistAsync(Guid nutritionistId, CancellationToken ct = default);

    /// <summary>
    /// Lista, en una sola consulta con joins y subconsultas, las filas de triaje de los
    /// pacientes activos asignados a un nutricionista (US18): puntaje IBS-SSS más reciente,
    /// última actividad y recomendaciones pendientes vencidas. El cálculo del nivel de
    /// prioridad y el orden final se realizan en la capa de aplicación.
    /// </summary>
    /// <param name="nutritionistId">Identificador del nutricionista.</param>
    /// <param name="utcNow">Marca de tiempo UTC para el corte de recomendaciones vencidas (24 h).</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Filas de triaje de los pacientes asignados.</returns>
    Task<IReadOnlyList<AssignedPatientTriageRow>> ListAssignedPatientTriageRowsAsync(
        Guid nutritionistId,
        DateTime utcNow,
        CancellationToken ct = default);

    /// <summary>
    /// Indica si existe una asignación activa entre un nutricionista y un paciente.
    /// </summary>
    /// <param name="nutritionistId">Identificador del nutricionista.</param>
    /// <param name="patientId">Identificador del paciente.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns><see langword="true"/> si la asignación activa existe.</returns>
    Task<bool> ActiveAssignmentExistsAsync(Guid nutritionistId, Guid patientId, CancellationToken ct = default);

    /// <summary>
    /// Agrega una nueva asignación al contexto de persistencia.
    /// </summary>
    /// <param name="assignment">Asignación a agregar.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Tarea que representa la operación asíncrona.</returns>
    Task AddAsync(NutritionistPatient assignment, CancellationToken ct = default);
}
