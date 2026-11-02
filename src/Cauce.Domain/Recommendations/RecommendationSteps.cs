namespace Cauce.Domain.Recommendations;

/// <summary>
/// Reglas de los pasos de aplicación de una recomendación: ninguno vacío y cada uno de 500 caracteres como máximo.
/// </summary>
public static class RecommendationSteps
{
    /// <summary>
    /// Largo máximo de un paso, el mismo que exige la creación manual.
    /// </summary>
    public const int MaxLength = 500;

    /// <summary>
    /// Valida los pasos y devuelve una copia, o <see langword="null"/> si no hay ninguno.
    /// </summary>
    /// <param name="steps">Pasos recibidos, o <see langword="null"/>.</param>
    /// <returns>Los pasos validados, o <see langword="null"/> si la lista es nula o vacía.</returns>
    /// <exception cref="ArgumentException">Si un paso es vacío o supera <see cref="MaxLength"/> caracteres.</exception>
    public static IReadOnlyList<string>? Normalize(IReadOnlyList<string>? steps)
    {
        if (steps is null || steps.Count == 0)
        {
            return null;
        }

        foreach (var step in steps)
        {
            if (string.IsNullOrWhiteSpace(step))
            {
                throw new ArgumentException("Los pasos de la recomendación no pueden estar vacíos.", nameof(steps));
            }

            if (step.Length > MaxLength)
            {
                throw new ArgumentException(
                    $"Cada paso de la recomendación admite como máximo {MaxLength} caracteres.", nameof(steps));
            }
        }

        return steps.ToArray();
    }
}
