using Cauce.Domain.Recommendations.Enums;

namespace Cauce.Application.Recommendations.Contracts;

/// <summary>
/// Resultado del orquestador de explicaciones: el texto en lenguaje natural y su origen.
/// </summary>
/// <param name="Text">Explicación en español dirigida al paciente.</param>
/// <param name="Source">Origen de la explicación (LLM o respaldo).</param>
public sealed record ExplanationResult(
    string Text,
    ExplanationSource Source);
