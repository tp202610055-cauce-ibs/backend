using Cauce.Application.Common.Interfaces.Patients;

namespace Cauce.Infrastructure.Patients;

/// <summary>
/// Implementación de <see cref="IBmiCalculator"/>. Calcula el índice de masa
/// corporal en kg/m² y lo categoriza según los umbrales de la OMS. No tiene estado,
/// por lo que es seguro registrarlo como singleton.
/// </summary>
public sealed class BmiCalculator : IBmiCalculator
{
    /// <inheritdoc />
    public decimal Calculate(decimal weightKg, decimal heightCm)
    {
        if (heightCm <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(heightCm));
        }

        var heightM = heightCm / 100m;
        var bmi = weightKg / (heightM * heightM);
        return Math.Round(bmi, 2, MidpointRounding.ToEven);
    }

    /// <inheritdoc />
    public string Categorize(decimal bmi) => bmi switch
    {
        < 18.5m => "underweight",
        < 25.0m => "normal",
        < 30.0m => "overweight",
        _ => "obese"
    };
}
