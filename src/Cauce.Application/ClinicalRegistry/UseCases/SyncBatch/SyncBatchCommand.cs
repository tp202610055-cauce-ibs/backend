using Cauce.Application.ClinicalRegistry.Dtos;
using Cauce.Domain.ClinicalRegistry.Enums;
using MediatR;

namespace Cauce.Application.ClinicalRegistry.UseCases.SyncBatch;

/// <summary>
/// Comando para sincronizar un lote de comidas y síntomas generados en el dispositivo.
/// Cada elemento se procesa individualmente con su propio <c>client_guid</c> a través
/// del pipeline de idempotencia.
/// </summary>
/// <param name="Meals">Comidas del lote.</param>
/// <param name="Symptoms">Síntomas del lote.</param>
public sealed record SyncBatchCommand(
    IReadOnlyList<MealBatchItem> Meals,
    IReadOnlyList<SymptomBatchItem> Symptoms) : IRequest<SyncBatchResult>;

/// <summary>
/// Comida dentro de un lote de sincronización.
/// </summary>
/// <param name="ClientGuid">Identificador estable del dispositivo (UUID v4).</param>
/// <param name="MealTime">Momento del día.</param>
/// <param name="ConsumedAt">Momento de consumo.</param>
/// <param name="ClientCreatedAt">Momento de creación en el dispositivo.</param>
/// <param name="Items">Ítems de la comida.</param>
public sealed record MealBatchItem(
    Guid ClientGuid,
    MealTime MealTime,
    DateTime ConsumedAt,
    DateTime ClientCreatedAt,
    IReadOnlyList<MealItemRequest> Items);

/// <summary>
/// Síntoma dentro de un lote de sincronización.
/// </summary>
/// <param name="ClientGuid">Identificador estable del dispositivo (UUID v4).</param>
/// <param name="SymptomType">Tipo de síntoma.</param>
/// <param name="Intensity">Intensidad (1–100).</param>
/// <param name="OccurredAt">Momento de ocurrencia.</param>
/// <param name="ClientCreatedAt">Momento de creación en el dispositivo.</param>
public sealed record SymptomBatchItem(
    Guid ClientGuid,
    SymptomType SymptomType,
    int Intensity,
    DateTime OccurredAt,
    DateTime ClientCreatedAt);
