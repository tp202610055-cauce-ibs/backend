namespace Cauce.Domain.ClinicalRegistry.Enums;

/// <summary>
/// Unidad de medida de la cantidad de un ítem de comida. En base de datos se
/// persiste como <c>varchar</c> en snake_case lowercase (<c>grams</c>, <c>cups</c>,
/// <c>units</c>, <c>ounces</c>, <c>tablespoons</c>).
/// </summary>
public enum MeasurementUnit
{
    /// <summary>
    /// Gramos.
    /// </summary>
    Grams = 0,

    /// <summary>
    /// Tazas.
    /// </summary>
    Cups = 1,

    /// <summary>
    /// Unidades (piezas).
    /// </summary>
    Units = 2,

    /// <summary>
    /// Onzas.
    /// </summary>
    Ounces = 3,

    /// <summary>
    /// Cucharadas.
    /// </summary>
    Tablespoons = 4
}
