using Cauce.Domain.ClinicalRegistry.Enums;

namespace Cauce.Application.ClinicalRegistry.UseCases.CreateMeal;

/// <summary>
/// Resultado del registro de una comida.
/// </summary>
/// <param name="MealId">Identificador de la comida creada.</param>
/// <param name="AggregatedFodmap">Carga FODMAP agregada de la comida.</param>
public sealed record CreateMealResult(Guid MealId, FodmapLevel AggregatedFodmap);
