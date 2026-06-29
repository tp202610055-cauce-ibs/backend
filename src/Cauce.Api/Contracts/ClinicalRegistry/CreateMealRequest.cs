using Cauce.Application.ClinicalRegistry.Dtos;
using Cauce.Domain.ClinicalRegistry.Enums;

namespace Cauce.Api.Contracts.ClinicalRegistry;

/// <summary>
/// Cuerpo de la petición de registro de una comida. El <see cref="ClientGuid"/> puede
/// omitirse en el cuerpo y enviarse en el encabezado <c>Idempotency-Key</c>.
/// </summary>
/// <param name="ClientGuid">Identificador del dispositivo (UUID v4), opcional si viaja en el encabezado.</param>
/// <param name="MealTime">Momento del día.</param>
/// <param name="ConsumedAt">Momento de consumo.</param>
/// <param name="ClientCreatedAt">Momento de creación en el dispositivo.</param>
/// <param name="Items">Ítems de la comida.</param>
public sealed record CreateMealRequest(
    Guid? ClientGuid,
    MealTime MealTime,
    DateTime ConsumedAt,
    DateTime ClientCreatedAt,
    IReadOnlyList<MealItemRequest> Items);
