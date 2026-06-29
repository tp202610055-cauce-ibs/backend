using Cauce.Application.ClinicalRegistry.Dtos;
using Cauce.Application.Common.Idempotency;
using Cauce.Domain.ClinicalRegistry.Enums;
using MediatR;

namespace Cauce.Application.ClinicalRegistry.UseCases.CreateMeal;

/// <summary>
/// Comando para registrar una comida del paciente autenticado. Es idempotente respecto
/// del <see cref="ClientGuid"/> generado en el dispositivo.
/// </summary>
/// <param name="ClientGuid">Identificador estable del dispositivo (UUID v4).</param>
/// <param name="MealTime">Momento del día.</param>
/// <param name="ConsumedAt">Momento de consumo.</param>
/// <param name="ClientCreatedAt">Momento de creación en el dispositivo.</param>
/// <param name="Items">Ítems de la comida (entre 1 y 50).</param>
public sealed record CreateMealCommand(
    Guid ClientGuid,
    MealTime MealTime,
    DateTime ConsumedAt,
    DateTime ClientCreatedAt,
    IReadOnlyList<MealItemRequest> Items) : IRequest<CreateMealResult>, IIdempotentCommand;
