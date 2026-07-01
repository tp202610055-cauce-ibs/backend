namespace Cauce.Application.Common.Interfaces.Recommendations;

/// <summary>
/// Lector de alergias del paciente para el guardrail duro pre-inferencia (DEC-B4-09). Devuelve
/// el conjunto de identificadores de alimentos prohibidos para el paciente. La detección es
/// heurística y conservadora hasta que exista el catálogo formal de mapeo (DEC-B4-14).
/// </summary>
public interface IPatientAllergyReader
{
    /// <summary>
    /// Obtiene el conjunto de identificadores de alimentos que el paciente debe evitar por sus
    /// alergias declaradas.
    /// </summary>
    /// <param name="patientId">Identificador del paciente.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>El conjunto de identificadores de alimentos prohibidos.</returns>
    Task<HashSet<Guid>> GetAllergyFoodIdsAsync(Guid patientId, CancellationToken ct = default);
}
