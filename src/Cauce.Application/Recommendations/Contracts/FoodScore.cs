namespace Cauce.Application.Recommendations.Contracts;

/// <summary>
/// Puntaje de un alimento producido por el motor: la probabilidad estimada de que cause
/// síntomas, en [0, 1], y un razonamiento opcional.
/// </summary>
/// <param name="FoodId">Identificador del alimento del catálogo.</param>
/// <param name="SymptomProbability">Probabilidad estimada de causar síntomas, en [0, 1].</param>
/// <param name="Reasoning">Razonamiento del puntaje, o <see langword="null"/>.</param>
public sealed record FoodScore(
    Guid FoodId,
    decimal SymptomProbability,
    string? Reasoning);
