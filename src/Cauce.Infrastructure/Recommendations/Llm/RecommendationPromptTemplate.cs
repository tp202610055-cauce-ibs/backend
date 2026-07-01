using Cauce.Application.Recommendations.Contracts;
using Cauce.Domain.Recommendations.Enums;

namespace Cauce.Infrastructure.Recommendations.Llm;

/// <summary>
/// Construye el prompt enviado a Ollama para generar la explicación de una recomendación. Cada
/// ítem se renderiza con su nombre, su acción en español y su razonamiento (aclaración técnica
/// del Bloque 4): el razonamiento se omite si está vacío y se trunca si supera los 120 caracteres.
/// </summary>
public static class RecommendationPromptTemplate
{
    private const int MaxReasoningLength = 120;
    private const int TruncatedReasoningLength = 117;

    /// <summary>
    /// Construye el prompt para el paciente y los ítems indicados.
    /// </summary>
    /// <param name="patient">Contexto clínico del paciente.</param>
    /// <param name="items">Ítems recomendados, con nombres legibles.</param>
    /// <returns>El prompt en español listo para enviar al modelo.</returns>
    public static string Build(PatientContextSnapshot patient, IReadOnlyList<ExplanationItem> items)
    {
        ArgumentNullException.ThrowIfNull(patient);
        ArgumentNullException.ThrowIfNull(items);

        var bulleted = string.Join(Environment.NewLine, items.Select(FormatItem));

        return
            "Eres un asistente nutricional que explica recomendaciones dietéticas a pacientes con " +
            "Síndrome de Intestino Irritable." + Environment.NewLine + Environment.NewLine +
            "Contexto del paciente:" + Environment.NewLine +
            $"- Subtipo SII: {patient.IbsSubtype}" + Environment.NewLine +
            "- Items recomendados:" + Environment.NewLine +
            bulleted + Environment.NewLine + Environment.NewLine +
            "Genera una explicación en español de máximo 4 oraciones que:" + Environment.NewLine +
            "1. Resuma de manera empática los cambios sugeridos." + Environment.NewLine +
            "2. Explique el patrón FODMAP general que motiva las sugerencias." + Environment.NewLine +
            "3. Evite afirmar diagnósticos, curas, garantías o certezas absolutas." + Environment.NewLine +
            "4. Use lenguaje probabilístico: \"puede\", \"podría\", \"se asocia con\"." + Environment.NewLine + Environment.NewLine +
            "Responde solo con la explicación, sin preámbulos.";
    }

    private static string FormatItem(ExplanationItem item)
    {
        var action = TranslateAction(item.ActionType);

        if (string.IsNullOrWhiteSpace(item.Reasoning))
        {
            return $"- {item.FoodName} ({action})";
        }

        var reasoning = item.Reasoning.Length > MaxReasoningLength
            ? string.Concat(item.Reasoning.AsSpan(0, TruncatedReasoningLength), "...")
            : item.Reasoning;

        return $"- {item.FoodName} ({action}): {reasoning}";
    }

    private static string TranslateAction(ActionType actionType)
    {
        return actionType switch
        {
            ActionType.Avoid => "evitar",
            ActionType.Reduce => "reducir",
            ActionType.Suggest => "incorporar",
            ActionType.Substitute => "sustituir",
            _ => "ajustar"
        };
    }
}
