using Cauce.Domain.ClinicalRegistry.Enums;

namespace Cauce.Application.ClinicalRegistry.Dtos;

/// <summary>
/// Síntoma del historial del paciente.
/// </summary>
/// <param name="SymptomId">Identificador del síntoma.</param>
/// <param name="ClientGuid">Identificador del dispositivo.</param>
/// <param name="SymptomType">Tipo de síntoma.</param>
/// <param name="Intensity">Intensidad (1–100).</param>
/// <param name="OccurredAt">Momento de ocurrencia.</param>
/// <param name="AssociatedMealId">Identificador de la comida asociada, o <see langword="null"/>.</param>
/// <param name="HasMealAssociation">Indica si hay asociación con una comida.</param>
/// <param name="SyncStatus">Estado de sincronización.</param>
public sealed record SymptomHistoryItem(
    Guid SymptomId,
    Guid ClientGuid,
    SymptomType SymptomType,
    int Intensity,
    DateTime OccurredAt,
    Guid? AssociatedMealId,
    bool HasMealAssociation,
    SyncStatus SyncStatus);
