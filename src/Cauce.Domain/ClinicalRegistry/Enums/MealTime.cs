namespace Cauce.Domain.ClinicalRegistry.Enums;

/// <summary>
/// Momento del día en que se consume una comida. En base de datos se persiste como
/// <c>varchar</c> en snake_case lowercase (<c>breakfast</c>, <c>lunch</c>,
/// <c>dinner</c>, <c>snack</c>).
/// </summary>
public enum MealTime
{
    /// <summary>
    /// Desayuno.
    /// </summary>
    Breakfast = 0,

    /// <summary>
    /// Almuerzo.
    /// </summary>
    Lunch = 1,

    /// <summary>
    /// Cena.
    /// </summary>
    Dinner = 2,

    /// <summary>
    /// Refrigerio o entrecomida.
    /// </summary>
    Snack = 3
}
