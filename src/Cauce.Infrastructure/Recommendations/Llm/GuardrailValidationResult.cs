namespace Cauce.Infrastructure.Recommendations.Llm;

/// <summary>
/// Resultado de la validación de guardrails clínicos de una explicación: si es válida y, en caso
/// contrario, la razón de la falla.
/// </summary>
/// <param name="IsValid">Indica si la explicación pasó los guardrails.</param>
/// <param name="FailureReason">Razón de la falla, o <see langword="null"/> si es válida.</param>
public sealed record GuardrailValidationResult(bool IsValid, string? FailureReason)
{
    /// <summary>
    /// Crea un resultado válido.
    /// </summary>
    /// <returns>Un resultado válido.</returns>
    public static GuardrailValidationResult Ok() => new(true, null);

    /// <summary>
    /// Crea un resultado fallido con la razón indicada.
    /// </summary>
    /// <param name="reason">Razón de la falla.</param>
    /// <returns>Un resultado fallido.</returns>
    public static GuardrailValidationResult Fail(string reason) => new(false, reason);
}
