using Cauce.Application.ClinicalRegistry.UseCases.SyncBatch;

namespace Cauce.Api.Contracts.ClinicalRegistry;

/// <summary>
/// Cuerpo de la petición de sincronización por lotes de comidas y síntomas.
/// </summary>
/// <param name="Meals">Comidas del lote.</param>
/// <param name="Symptoms">Síntomas del lote.</param>
public sealed record SyncBatchRequest(
    IReadOnlyList<MealBatchItem> Meals,
    IReadOnlyList<SymptomBatchItem> Symptoms);
