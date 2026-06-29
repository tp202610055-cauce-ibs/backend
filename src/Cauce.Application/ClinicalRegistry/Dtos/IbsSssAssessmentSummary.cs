using Cauce.Domain.ClinicalRegistry.Enums;

namespace Cauce.Application.ClinicalRegistry.Dtos;

/// <summary>
/// Resumen de una evaluación IBS-SSS.
/// </summary>
/// <param name="AssessmentId">Identificador de la evaluación.</param>
/// <param name="AssessmentType">Tipo de evaluación.</param>
/// <param name="CycleNumber">Número de ciclo.</param>
/// <param name="PainSeverity">Severidad del dolor.</param>
/// <param name="PainFrequency">Frecuencia del dolor.</param>
/// <param name="BloatingSeverity">Severidad de la distensión.</param>
/// <param name="BowelHabitsDissatisfaction">Insatisfacción con el hábito intestinal.</param>
/// <param name="LifeInterference">Interferencia con la vida diaria.</param>
/// <param name="TotalScore">Puntaje total (0–500).</param>
/// <param name="SeverityCategory">Categoría de severidad.</param>
/// <param name="CompletedAt">Momento de finalización, en UTC.</param>
/// <param name="NextAssessmentDate">Fecha sugerida de la próxima evaluación.</param>
public sealed record IbsSssAssessmentSummary(
    Guid AssessmentId,
    AssessmentType AssessmentType,
    int CycleNumber,
    int PainSeverity,
    int PainFrequency,
    int BloatingSeverity,
    int BowelHabitsDissatisfaction,
    int LifeInterference,
    int TotalScore,
    SeverityCategory SeverityCategory,
    DateTime CompletedAt,
    DateOnly? NextAssessmentDate);

/// <summary>
/// Entrada de la evolución IBS-SSS: una evaluación junto con su diferencia de puntaje
/// respecto de la línea base (negativa indica mejoría).
/// </summary>
/// <param name="Assessment">Evaluación.</param>
/// <param name="DeltaFromBaseline">Diferencia con la línea base, o <see langword="null"/> para la propia línea base.</param>
public sealed record IbsSssEvolutionEntry(IbsSssAssessmentSummary Assessment, int? DeltaFromBaseline);
