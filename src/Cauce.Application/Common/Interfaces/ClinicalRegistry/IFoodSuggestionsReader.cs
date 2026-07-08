using Cauce.Application.ClinicalRegistry.Dtos;

namespace Cauce.Application.Common.Interfaces.ClinicalRegistry;

/// <summary>
/// Lee las sugerencias de alimentos del paciente (US09 CA03): frecuentes, recientes y una selección
/// rotativa del catálogo determinista por semana.
/// </summary>
public interface IFoodSuggestionsReader
{
    /// <summary>
    /// Obtiene las tres listas de sugerencias de alimentos del paciente.
    /// </summary>
    /// <param name="patientId">Identificador del paciente.</param>
    /// <param name="utcNow">Marca de tiempo UTC de referencia para las ventanas de 30 días y 24 horas.</param>
    /// <param name="catalogSeed">Semilla determinista (paciente + semana del año) para la selección del catálogo.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Las sugerencias de alimentos.</returns>
    Task<FoodSuggestionsResult> GetAsync(
        Guid patientId,
        DateTime utcNow,
        int catalogSeed,
        CancellationToken ct = default);
}
