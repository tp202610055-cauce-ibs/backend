using System.Text.Json.Serialization;

namespace Cauce.Application.ClinicalRegistry.Dtos;

/// <summary>
/// Evento del historial unificado del paciente. Es la base de una unión discriminada
/// serializada con el campo <c>eventType</c> (<c>meal</c>, <c>symptom</c>,
/// <c>clinical_note</c>). Los eventos se ordenan cronológicamente de forma descendente.
/// </summary>
/// <param name="OccurredAt">Momento cronológico del evento (UTC) usado para ordenar.</param>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "eventType")]
[JsonDerivedType(typeof(MealHistoryEvent), "meal")]
[JsonDerivedType(typeof(SymptomHistoryEvent), "symptom")]
[JsonDerivedType(typeof(ClinicalNoteHistoryEvent), "clinical_note")]
public abstract record HistoryEvent(DateTime OccurredAt);

/// <summary>
/// Evento de historial correspondiente a una comida.
/// </summary>
/// <param name="OccurredAt">Momento de consumo de la comida.</param>
/// <param name="Meal">Datos de la comida.</param>
public sealed record MealHistoryEvent(DateTime OccurredAt, MealHistoryItem Meal) : HistoryEvent(OccurredAt);

/// <summary>
/// Evento de historial correspondiente a un síntoma.
/// </summary>
/// <param name="OccurredAt">Momento de ocurrencia del síntoma.</param>
/// <param name="Symptom">Datos del síntoma.</param>
public sealed record SymptomHistoryEvent(DateTime OccurredAt, SymptomHistoryItem Symptom) : HistoryEvent(OccurredAt);

/// <summary>
/// Evento de historial correspondiente a una nota clínica.
/// </summary>
/// <param name="OccurredAt">Momento de creación de la nota.</param>
/// <param name="Note">Datos de la nota clínica.</param>
public sealed record ClinicalNoteHistoryEvent(DateTime OccurredAt, ClinicalNoteSummary Note) : HistoryEvent(OccurredAt);
