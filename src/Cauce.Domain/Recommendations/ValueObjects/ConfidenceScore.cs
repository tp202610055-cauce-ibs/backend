using Cauce.Domain.Common;
using Cauce.Domain.Recommendations.Exceptions;

namespace Cauce.Domain.Recommendations.ValueObjects;

/// <summary>
/// Puntaje de confianza del motor sobre una recomendación, en el rango [0, 1]. Se redondea
/// a tres decimales y su igualdad se determina por valor.
/// </summary>
public sealed class ConfidenceScore : ValueObject
{
    private ConfidenceScore(decimal value)
    {
        Value = value;
    }

    /// <summary>
    /// Valor del puntaje, en el rango [0, 1], redondeado a tres decimales.
    /// </summary>
    public decimal Value { get; }

    /// <summary>
    /// Crea un puntaje de confianza validando su rango y redondeándolo a tres decimales.
    /// </summary>
    /// <param name="value">Valor en el rango [0, 1].</param>
    /// <returns>El puntaje de confianza.</returns>
    /// <exception cref="InvalidConfidenceScoreException">Si el valor está fuera de [0, 1].</exception>
    public static ConfidenceScore Create(decimal value)
    {
        if (value < 0m || value > 1m)
        {
            throw new InvalidConfidenceScoreException(value);
        }

        return new ConfidenceScore(Math.Round(value, 3, MidpointRounding.AwayFromZero));
    }

    /// <inheritdoc />
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}
