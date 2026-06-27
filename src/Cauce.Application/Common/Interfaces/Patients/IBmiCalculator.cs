namespace Cauce.Application.Common.Interfaces.Patients;

/// <summary>
/// Servicio de cálculo y categorización del índice de masa corporal (IMC). El
/// servidor siempre recalcula el IMC; nunca acepta el valor desde el cliente.
/// </summary>
public interface IBmiCalculator
{
    /// <summary>
    /// Calcula el IMC a partir del peso y la estatura.
    /// </summary>
    /// <param name="weightKg">Peso en kilogramos.</param>
    /// <param name="heightCm">Estatura en centímetros.</param>
    /// <returns>El IMC en kg/m², redondeado a dos decimales.</returns>
    decimal Calculate(decimal weightKg, decimal heightCm);

    /// <summary>
    /// Devuelve la categoría del IMC según los umbrales de la OMS.
    /// </summary>
    /// <param name="bmi">Valor del IMC.</param>
    /// <returns>La categoría textual del IMC.</returns>
    string Categorize(decimal bmi);
}
