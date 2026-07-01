using Cauce.Application.Recommendations.Contracts;
using Cauce.Domain.Recommendations.Enums;

namespace Cauce.Infrastructure.Recommendations.Llm;

/// <summary>
/// Genera una explicación de respaldo mediante una plantilla estática en español cuando el LLM
/// falla, agota su tiempo o no pasa los guardrails (DEC-B4-07). No tiene pretensión generativa:
/// enumera de forma agregada las acciones sugeridas.
/// </summary>
public sealed class FallbackExplanationProvider
{
    /// <summary>
    /// Genera la explicación de respaldo para los ítems indicados.
    /// </summary>
    /// <param name="items">Ítems de la recomendación.</param>
    /// <returns>La explicación de respaldo con origen <see cref="ExplanationSource.Fallback"/>.</returns>
    public ExplanationResult GenerateFor(IReadOnlyList<ExplanationItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var avoidCount = items.Count(item => item.ActionType == ActionType.Avoid);
        var substituteCount = items.Count(item => item.ActionType == ActionType.Substitute);
        var reduceCount = items.Count(item => item.ActionType == ActionType.Reduce);
        var suggestCount = items.Count(item => item.ActionType == ActionType.Suggest);

        var parts = new List<string>();
        if (avoidCount > 0)
        {
            parts.Add($"evitar {avoidCount} alimento(s)");
        }

        if (substituteCount > 0)
        {
            parts.Add($"sustituir {substituteCount} alimento(s)");
        }

        if (reduceCount > 0)
        {
            parts.Add($"reducir {reduceCount} alimento(s)");
        }

        if (suggestCount > 0)
        {
            parts.Add($"incorporar {suggestCount} alimento(s)");
        }

        var summary = parts.Count > 0 ? string.Join(", ", parts) : "ajustar la dieta";

        var text =
            $"Se sugiere {summary} en función del perfil FODMAP identificado. " +
            "Estas recomendaciones pueden ayudar a reducir la incidencia de síntomas, aunque la " +
            "respuesta individual varía. Si tu nutricionista ajusta esta recomendación, confía en su " +
            "criterio profesional.";

        return new ExplanationResult(text, ExplanationSource.Fallback);
    }
}
