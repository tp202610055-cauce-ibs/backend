using Cauce.Domain.ClinicalRegistry.Enums;
using Cauce.Domain.ClinicalRegistry.Exceptions;
using Cauce.Domain.Common;

namespace Cauce.Domain.ClinicalRegistry;

/// <summary>
/// Evaluación IBS-SSS (Irritable Bowel Syndrome — Severity Scoring System) del
/// paciente. Es raíz de agregado e inmutable tras su creación. El servidor siempre
/// recalcula el puntaje total y la categoría a partir de las cinco dimensiones; nunca
/// los acepta del cliente. Cada paciente puede tener a lo sumo una evaluación de línea
/// base. Es la métrica primaria del piloto clínico.
/// </summary>
public sealed class IbsSssAssessment : Entity, IAggregateRoot
{
    private const int AssessmentIntervalDays = 14;

    /// <summary>
    /// Identificador del paciente evaluado.
    /// </summary>
    public Guid PatientId { get; private set; }

    /// <summary>
    /// Tipo de evaluación (línea base o periódica).
    /// </summary>
    public AssessmentType AssessmentType { get; private set; }

    /// <summary>
    /// Número de ciclo de la evaluación. Cero para la línea base; correlativo positivo
    /// para las periódicas.
    /// </summary>
    public int CycleNumber { get; private set; }

    /// <summary>
    /// Dimensión: severidad del dolor abdominal (0–100).
    /// </summary>
    public int PainSeverity { get; private set; }

    /// <summary>
    /// Dimensión: frecuencia del dolor abdominal (0–100).
    /// </summary>
    public int PainFrequency { get; private set; }

    /// <summary>
    /// Dimensión: severidad de la distensión abdominal (0–100).
    /// </summary>
    public int BloatingSeverity { get; private set; }

    /// <summary>
    /// Dimensión: insatisfacción con el hábito intestinal (0–100).
    /// </summary>
    public int BowelHabitsDissatisfaction { get; private set; }

    /// <summary>
    /// Dimensión: interferencia con la vida diaria (0–100).
    /// </summary>
    public int LifeInterference { get; private set; }

    /// <summary>
    /// Puntaje total IBS-SSS (0–500), calculado por el servidor.
    /// </summary>
    public int TotalScore { get; private set; }

    /// <summary>
    /// Categoría de severidad derivada del puntaje total.
    /// </summary>
    public SeverityCategory SeverityCategory { get; private set; }

    /// <summary>
    /// Momento de finalización de la evaluación, en UTC.
    /// </summary>
    public DateTime CompletedAt { get; private set; }

    /// <summary>
    /// Fecha sugerida de la próxima evaluación (14 días después), o <see langword="null"/>.
    /// </summary>
    public DateOnly? NextAssessmentDate { get; private set; }

    private IbsSssAssessment()
    {
    }

    private IbsSssAssessment(
        Guid id,
        Guid patientId,
        AssessmentType assessmentType,
        int cycleNumber,
        int painSeverity,
        int painFrequency,
        int bloatingSeverity,
        int bowelHabitsDissatisfaction,
        int lifeInterference,
        int totalScore,
        SeverityCategory severityCategory,
        DateTime utcNow)
        : base(id)
    {
        PatientId = patientId;
        AssessmentType = assessmentType;
        CycleNumber = cycleNumber;
        PainSeverity = painSeverity;
        PainFrequency = painFrequency;
        BloatingSeverity = bloatingSeverity;
        BowelHabitsDissatisfaction = bowelHabitsDissatisfaction;
        LifeInterference = lifeInterference;
        TotalScore = totalScore;
        SeverityCategory = severityCategory;
        CompletedAt = utcNow;
        NextAssessmentDate = DateOnly.FromDateTime(utcNow).AddDays(AssessmentIntervalDays);
    }

    /// <summary>
    /// Envía una evaluación IBS-SSS: valida las dimensiones y el ciclo, calcula el
    /// puntaje total y la categoría de severidad, y fija la fecha de la próxima evaluación.
    /// </summary>
    /// <param name="id">Identificador de la evaluación.</param>
    /// <param name="patientId">Identificador del paciente.</param>
    /// <param name="type">Tipo de evaluación.</param>
    /// <param name="cycleNumber">Número de ciclo (0 para línea base, ≥1 para periódica).</param>
    /// <param name="painSeverity">Severidad del dolor (0–100).</param>
    /// <param name="painFrequency">Frecuencia del dolor (0–100).</param>
    /// <param name="bloatingSeverity">Severidad de la distensión (0–100).</param>
    /// <param name="bowelHabitsDissatisfaction">Insatisfacción con el hábito intestinal (0–100).</param>
    /// <param name="lifeInterference">Interferencia con la vida diaria (0–100).</param>
    /// <param name="utcNow">Marca de tiempo UTC de finalización.</param>
    /// <returns>La nueva evaluación.</returns>
    /// <exception cref="InvalidIbsSssDimensionException">Si una dimensión o el número de ciclo viola sus rangos.</exception>
    public static IbsSssAssessment Submit(
        Guid id,
        Guid patientId,
        AssessmentType type,
        int cycleNumber,
        int painSeverity,
        int painFrequency,
        int bloatingSeverity,
        int bowelHabitsDissatisfaction,
        int lifeInterference,
        DateTime utcNow)
    {
        if (patientId == Guid.Empty)
        {
            throw new InvalidIbsSssDimensionException("El identificador del paciente es obligatorio.");
        }

        EnsureDimensionInRange(painSeverity, "pain_severity");
        EnsureDimensionInRange(painFrequency, "pain_frequency");
        EnsureDimensionInRange(bloatingSeverity, "bloating_severity");
        EnsureDimensionInRange(bowelHabitsDissatisfaction, "bowel_habits_dissatisfaction");
        EnsureDimensionInRange(lifeInterference, "life_interference");

        if (type == AssessmentType.Baseline && cycleNumber != 0)
        {
            throw new InvalidIbsSssDimensionException("La evaluación de línea base debe tener número de ciclo cero.");
        }

        if (type == AssessmentType.Periodic && cycleNumber < 1)
        {
            throw new InvalidIbsSssDimensionException("La evaluación periódica debe tener número de ciclo mayor o igual a uno.");
        }

        var totalScore = IbsSssScoring.CalculateTotal(
            painSeverity, painFrequency, bloatingSeverity, bowelHabitsDissatisfaction, lifeInterference);
        var severityCategory = IbsSssScoring.Categorize(totalScore);

        return new IbsSssAssessment(
            id, patientId, type, cycleNumber, painSeverity, painFrequency, bloatingSeverity,
            bowelHabitsDissatisfaction, lifeInterference, totalScore, severityCategory, utcNow);
    }

    /// <summary>
    /// Compara el puntaje total de esta evaluación con el de otra previa.
    /// </summary>
    /// <param name="previous">Evaluación previa.</param>
    /// <returns>La diferencia entera (negativa indica mejoría respecto de la previa).</returns>
    public int CompareTotalScoreTo(IbsSssAssessment previous)
    {
        ArgumentNullException.ThrowIfNull(previous);
        return TotalScore - previous.TotalScore;
    }

    /// <summary>
    /// Indica si esta evaluación representa una mejoría clínicamente significativa
    /// respecto de la línea base (reducción de al menos 50 puntos).
    /// </summary>
    /// <param name="baseline">Evaluación de línea base.</param>
    /// <returns><see langword="true"/> si la reducción es de 50 puntos o más.</returns>
    public bool IsClinicallySignificantImprovement(IbsSssAssessment baseline)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        return TotalScore <= baseline.TotalScore - IbsSssScoring.ClinicallySignificantDelta;
    }

    private static void EnsureDimensionInRange(int value, string dimensionName)
    {
        if (value is < IbsSssScoring.DimensionMin or > IbsSssScoring.DimensionMax)
        {
            throw new InvalidIbsSssDimensionException($"La dimensión '{dimensionName}' debe estar entre {IbsSssScoring.DimensionMin} y {IbsSssScoring.DimensionMax}.");
        }
    }
}
