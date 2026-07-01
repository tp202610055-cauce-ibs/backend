using Cauce.Application.Recommendations.Contracts;

namespace Cauce.Application.Common.Interfaces.Recommendations;

/// <summary>
/// Lector del historial clínico del paciente para construir el conjunto de alimentos
/// candidatos. Lee directamente las tablas de comidas y del catálogo (DEC-B4-03), sin acoplar
/// el módulo de recomendaciones a los repositorios del módulo de registro clínico.
/// </summary>
public interface IPatientClinicalHistoryReader
{
    /// <summary>
    /// Obtiene los alimentos del catálogo que el paciente consumió en la ventana indicada,
    /// distintos y con su cantidad promedio aproximada.
    /// </summary>
    /// <param name="patientId">Identificador del paciente.</param>
    /// <param name="windowDays">Tamaño de la ventana de consumo, en días.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Los alimentos candidatos.</returns>
    Task<IReadOnlyList<CandidateFood>> GetCandidateFoodsAsync(
        Guid patientId,
        int windowDays,
        CancellationToken ct = default);
}
