using System.Text.RegularExpressions;
using Cauce.Application.Recommendations.Configuration;
using Microsoft.Extensions.Options;

namespace Cauce.Infrastructure.Recommendations.Llm;

/// <summary>
/// Valida que la explicación generada por el LLM cumpla los guardrails clínicos (DEC-B4-07): sin
/// términos clínicos prohibidos, sin certezas absolutas, sin causalidad directa, dentro de los
/// límites de longitud y en español. Si no pasa, el orquestador cae a la plantilla de respaldo.
/// </summary>
public sealed partial class RecommendationGuardrailsValidator
{
    private const int MinSpanishMarkers = 3;

    private readonly OllamaOptions _ollamaOptions;

    /// <summary>
    /// Inicializa el validador con las opciones de longitud.
    /// </summary>
    /// <param name="options">Opciones del módulo de recomendaciones.</param>
    public RecommendationGuardrailsValidator(IOptions<RecommendationsOptions> options)
    {
        _ollamaOptions = options.Value.Ollama;
    }

    /// <summary>
    /// Valida el texto de una explicación.
    /// </summary>
    /// <param name="text">Texto a validar.</param>
    /// <returns>El resultado de la validación.</returns>
    public GuardrailValidationResult Validate(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return GuardrailValidationResult.Fail("empty_or_whitespace");
        }

        if (text.Length < _ollamaOptions.MinOutputCharacters)
        {
            return GuardrailValidationResult.Fail("too_short");
        }

        if (text.Length > _ollamaOptions.MaxOutputCharacters)
        {
            return GuardrailValidationResult.Fail("too_long");
        }

        if (ForbiddenClinical().IsMatch(text))
        {
            return GuardrailValidationResult.Fail("contains_clinical_term");
        }

        if (ForbiddenCertainty().IsMatch(text))
        {
            return GuardrailValidationResult.Fail("contains_certainty_term");
        }

        if (ForbiddenCausal().IsMatch(text))
        {
            return GuardrailValidationResult.Fail("contains_causal_claim");
        }

        if (SpanishMarkers().Matches(text).Count < MinSpanishMarkers)
        {
            return GuardrailValidationResult.Fail("not_spanish");
        }

        return GuardrailValidationResult.Ok();
    }

    [GeneratedRegex(@"\b(diagn[oó]stico|curar?|garanti[zs]ar?|s[ií]ndrome|enfermedad)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ForbiddenClinical();

    [GeneratedRegex(@"\b(siempre|nunca|sin duda|definitivamente)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ForbiddenCertainty();

    [GeneratedRegex(@"\b(te causa|te va a causar|te genera s[ií]ntomas)\b", RegexOptions.IgnoreCase)]
    private static partial Regex ForbiddenCausal();

    [GeneratedRegex(@"\b(el|la|los|las|para|con|sin|por|que)\b", RegexOptions.IgnoreCase)]
    private static partial Regex SpanishMarkers();
}
