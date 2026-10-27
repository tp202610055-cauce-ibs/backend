using Cauce.Application.ClinicalRegistry.Dtos;
using Cauce.Application.ClinicalRegistry.Mapping;
using Cauce.Application.Common.Interfaces.ClinicalRegistry;

namespace Cauce.Application.ClinicalRegistry.Services;

/// <summary>
/// Arma el historial unificado de un paciente (comidas, síntomas y notas clínicas) en un rango, en
/// orden cronológico descendente. Lo comparten la vista del paciente y la del nutricionista.
/// </summary>
public static class UnifiedHistoryReader
{
    /// <summary>
    /// Tope de comidas y de síntomas por consulta; las notas no tienen tope.
    /// </summary>
    public const int MaxPerSource = 500;

    /// <summary>
    /// Lee el historial del paciente en el rango indicado.
    /// </summary>
    public static async Task<IReadOnlyList<HistoryEvent>> ReadAsync(
        Guid patientId,
        DateTime from,
        DateTime to,
        IMealRepository mealRepository,
        ISymptomRepository symptomRepository,
        IClinicalNoteRepository clinicalNoteRepository,
        IMealFodmapResolver fodmapResolver,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(mealRepository);
        ArgumentNullException.ThrowIfNull(symptomRepository);
        ArgumentNullException.ThrowIfNull(clinicalNoteRepository);
        ArgumentNullException.ThrowIfNull(fodmapResolver);

        var meals = await mealRepository
            .ListByPatientInRangeAsync(patientId, from, to, 0, MaxPerSource, ct)
            .ConfigureAwait(false);

        var symptoms = await symptomRepository
            .ListByPatientInRangeAsync(patientId, from, to, 0, MaxPerSource, ct)
            .ConfigureAwait(false);

        var notes = await clinicalNoteRepository
            .ListByPatientInRangeAsync(patientId, from, to, ct)
            .ConfigureAwait(false);

        var events = new List<HistoryEvent>(meals.Count + symptoms.Count + notes.Count);
        var fodmapByMeal = await fodmapResolver.ResolveAsync(meals, ct).ConfigureAwait(false);
        events.AddRange(meals.Select(meal => new MealHistoryEvent(
            meal.ConsumedAt,
            ClinicalRegistryMappings.ToHistoryItem(
                meal,
                fodmapByMeal.TryGetValue(meal.Id, out var level) ? level : null))));
        events.AddRange(symptoms.Select(symptom => new SymptomHistoryEvent(symptom.OccurredAt, ClinicalRegistryMappings.ToHistoryItem(symptom))));
        events.AddRange(notes.Select(note => new ClinicalNoteHistoryEvent(note.CreatedAt, ClinicalRegistryMappings.ToSummary(note))));

        return events
            .OrderByDescending(historyEvent => historyEvent.OccurredAt)
            .ToList();
    }
}
