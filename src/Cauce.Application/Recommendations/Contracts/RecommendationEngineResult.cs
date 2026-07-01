using Cauce.Domain.Recommendations.ValueObjects;

namespace Cauce.Application.Recommendations.Contracts;

/// <summary>
/// Resultado del motor de recomendaciones: el puntaje por alimento, la confianza agregada y
/// el descriptor de la versión usada.
/// </summary>
/// <param name="Scores">Puntaje de cada alimento evaluado.</param>
/// <param name="AggregateConfidence">Confianza agregada sobre las recomendaciones accionables.</param>
/// <param name="ModelUsed">Descriptor de la versión del motor que produjo el resultado.</param>
public sealed record RecommendationEngineResult(
    IReadOnlyList<FoodScore> Scores,
    ConfidenceScore AggregateConfidence,
    ModelVersionDescriptor ModelUsed);
