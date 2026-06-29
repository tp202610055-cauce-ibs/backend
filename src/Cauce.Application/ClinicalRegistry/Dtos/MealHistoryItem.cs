using Cauce.Domain.ClinicalRegistry.Enums;

namespace Cauce.Application.ClinicalRegistry.Dtos;

/// <summary>
/// Resumen de un ítem de comida en el historial.
/// </summary>
/// <param name="FoodId">Identificador del alimento del catálogo, o <see langword="null"/>.</param>
/// <param name="CustomFoodId">Identificador del alimento personalizado, o <see langword="null"/>.</param>
/// <param name="Quantity">Cantidad consumida.</param>
/// <param name="Unit">Unidad de medida.</param>
public sealed record MealItemSummary(Guid? FoodId, Guid? CustomFoodId, decimal Quantity, MeasurementUnit Unit);

/// <summary>
/// Comida del historial del paciente, con sus ítems y, opcionalmente, su carga FODMAP
/// agregada.
/// </summary>
/// <param name="MealId">Identificador de la comida.</param>
/// <param name="ClientGuid">Identificador del dispositivo.</param>
/// <param name="MealTime">Momento del día.</param>
/// <param name="ConsumedAt">Momento de consumo.</param>
/// <param name="ClientCreatedAt">Momento de creación en el dispositivo.</param>
/// <param name="SyncStatus">Estado de sincronización.</param>
/// <param name="Items">Ítems de la comida.</param>
/// <param name="AggregatedFodmap">Carga FODMAP agregada, o <see langword="null"/> si no se solicitó.</param>
public sealed record MealHistoryItem(
    Guid MealId,
    Guid ClientGuid,
    MealTime MealTime,
    DateTime ConsumedAt,
    DateTime ClientCreatedAt,
    SyncStatus SyncStatus,
    IReadOnlyList<MealItemSummary> Items,
    FodmapLevel? AggregatedFodmap);
