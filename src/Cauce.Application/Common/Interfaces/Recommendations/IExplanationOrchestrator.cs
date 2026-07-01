using Cauce.Application.Recommendations.Contracts;

namespace Cauce.Application.Common.Interfaces.Recommendations;

/// <summary>
/// Orquestador de explicaciones: convierte los ítems de una recomendación en una explicación
/// en lenguaje natural para el paciente, invocando al LLM local y cayendo a una plantilla de
/// respaldo si el LLM falla o no pasa los guardrails clínicos (DEC-B4-07).
/// </summary>
public interface IExplanationOrchestrator
{
    /// <summary>
    /// Genera la explicación de una recomendación.
    /// </summary>
    /// <param name="patient">Contexto clínico del paciente.</param>
    /// <param name="items">Ítems a explicar, con nombres legibles de alimentos.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>La explicación generada y su origen.</returns>
    Task<ExplanationResult> ExplainAsync(
        PatientContextSnapshot patient,
        IReadOnlyList<ExplanationItem> items,
        CancellationToken cancellationToken);
}
