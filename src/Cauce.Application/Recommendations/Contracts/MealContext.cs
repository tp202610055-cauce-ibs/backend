namespace Cauce.Application.Recommendations.Contracts;

/// <summary>
/// Contexto de la comida para la que se generan recomendaciones. En Prompt 4 es un valor
/// fijo de marcador de posición hasta que exista la programación por comida.
/// </summary>
/// <param name="MealTime">Momento del día de la comida.</param>
public sealed record MealContext(string MealTime);
