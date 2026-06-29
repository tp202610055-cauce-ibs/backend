using Cauce.Domain.ClinicalRegistry;

namespace Cauce.Application.Common.Interfaces.ClinicalRegistry;

/// <summary>
/// Repositorio del agregado <see cref="Symptom"/> (síntomas).
/// </summary>
public interface ISymptomRepository
{
    /// <summary>
    /// Busca un síntoma por su identificador.
    /// </summary>
    /// <param name="symptomId">Identificador del síntoma.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>El síntoma o <see langword="null"/> si no existe.</returns>
    Task<Symptom?> FindByIdAsync(Guid symptomId, CancellationToken ct = default);

    /// <summary>
    /// Busca un síntoma por su <c>client_guid</c> (identidad estable del dispositivo).
    /// </summary>
    /// <param name="clientGuid">Identificador del dispositivo.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>El síntoma o <see langword="null"/> si no existe.</returns>
    Task<Symptom?> FindByClientGuidAsync(Guid clientGuid, CancellationToken ct = default);

    /// <summary>
    /// Lista los síntomas del paciente en un rango de fechas, paginados.
    /// </summary>
    /// <param name="patientId">Identificador del paciente.</param>
    /// <param name="from">Inicio del rango.</param>
    /// <param name="to">Fin del rango.</param>
    /// <param name="skip">Cantidad de elementos a omitir.</param>
    /// <param name="take">Cantidad de elementos a tomar.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>La página de síntomas.</returns>
    Task<IReadOnlyList<Symptom>> ListByPatientInRangeAsync(Guid patientId, DateTime from, DateTime to, int skip, int take, CancellationToken ct = default);

    /// <summary>
    /// Cuenta los síntomas del paciente en un rango de fechas.
    /// </summary>
    /// <param name="patientId">Identificador del paciente.</param>
    /// <param name="from">Inicio del rango.</param>
    /// <param name="to">Fin del rango.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>La cantidad de síntomas.</returns>
    Task<int> CountByPatientInRangeAsync(Guid patientId, DateTime from, DateTime to, CancellationToken ct = default);

    /// <summary>
    /// Agrega un nuevo síntoma al contexto de persistencia.
    /// </summary>
    /// <param name="symptom">Síntoma a agregar.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Tarea que representa la operación asíncrona.</returns>
    Task AddAsync(Symptom symptom, CancellationToken ct = default);
}
