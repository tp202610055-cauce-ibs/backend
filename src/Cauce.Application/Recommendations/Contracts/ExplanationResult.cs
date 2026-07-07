using Cauce.Domain.Recommendations.Enums;

namespace Cauce.Application.Recommendations.Contracts;

/// <summary>
/// Resultado del orquestador de explicaciones: el texto en lenguaje natural, su origen y, cuando se
/// activó el respaldo, el diagnóstico del fallback para su auditoría (TS08 CA02).
/// </summary>
/// <param name="Text">Explicación en español dirigida al paciente.</param>
/// <param name="Source">Origen de la explicación (LLM o respaldo).</param>
/// <param name="FallbackReason">Motivo del fallback (<c>timeout</c> o <c>error</c>), o <see langword="null"/> si no hubo fallback.</param>
/// <param name="FallbackErrorType">Detalle del error que causó el fallback, o <see langword="null"/>.</param>
/// <param name="FallbackDurationMs">Duración en milisegundos de la invocación al modelo antes del fallback, o <see langword="null"/>.</param>
public sealed record ExplanationResult(
    string Text,
    ExplanationSource Source,
    string? FallbackReason = null,
    string? FallbackErrorType = null,
    long? FallbackDurationMs = null);
