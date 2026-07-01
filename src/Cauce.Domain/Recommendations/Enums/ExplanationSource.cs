namespace Cauce.Domain.Recommendations.Enums;

/// <summary>
/// Origen de la explicación en lenguaje natural de una recomendación. En base de datos
/// se persiste como <c>varchar</c> en snake_case (por ejemplo, <c>"llm_generated"</c>).
/// </summary>
public enum ExplanationSource
{
    /// <summary>
    /// Generada por el modelo de lenguaje (Ollama) y validada por los guardrails clínicos.
    /// </summary>
    LlmGenerated,

    /// <summary>
    /// Generada por la plantilla estática de respaldo cuando el LLM falla o no pasa los
    /// guardrails.
    /// </summary>
    Fallback
}
