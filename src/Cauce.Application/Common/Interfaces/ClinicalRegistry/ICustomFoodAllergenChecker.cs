using Cauce.Domain.ClinicalRegistry;

namespace Cauce.Application.Common.Interfaces.ClinicalRegistry;

/// <summary>
/// Detecta coincidencias entre los ingredientes de un alimento personalizado y las alergias declaradas
/// por el paciente (US10 CA03). Reutiliza el matcher heurístico conservador (DEC-B4-14) evaluándolo por
/// alergia individual para poder identificar qué alergia coincide con qué ingrediente.
/// </summary>
public interface ICustomFoodAllergenChecker
{
    /// <summary>
    /// Cruza los ingredientes del catálogo indicados contra las alergias del paciente y devuelve las
    /// coincidencias detectadas.
    /// </summary>
    /// <param name="patientId">Identificador del paciente.</param>
    /// <param name="ingredientFoodIds">Identificadores de los alimentos del catálogo usados como ingredientes.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Las coincidencias alérgeno-ingrediente detectadas; lista vacía si no hay ninguna.</returns>
    Task<IReadOnlyList<DetectedAllergen>> CheckAsync(
        Guid patientId,
        IReadOnlyCollection<Guid> ingredientFoodIds,
        CancellationToken ct = default);
}
