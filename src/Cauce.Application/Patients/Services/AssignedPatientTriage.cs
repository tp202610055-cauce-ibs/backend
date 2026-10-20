using Cauce.Application.Patients.Dtos;
using Cauce.Domain.Patients.Enums;

namespace Cauce.Application.Patients.Services;

/// <summary>
/// Prioridad y orden del panel de triaje según HU0018: alta, media y el resto, con el reloj recibido
/// como parámetro.
/// </summary>
public static class AssignedPatientTriage
{
    /// <summary>
    /// Puntaje IBS-SSS a partir del cual, sin incluirlo, el paciente pasa a prioridad alta.
    /// </summary>
    public const int HighIbsSssScoreThreshold = 300;

    /// <summary>
    /// Horas sin registros de comida ni de síntoma a partir de las cuales el paciente pasa a prioridad media.
    /// </summary>
    public const int InactivityHours = 72;

    /// <summary>
    /// Proyecta las filas del repositorio y las ordena: prioridad, luego puntaje IBS-SSS y luego antigüedad del vínculo.
    /// </summary>
    /// <param name="rows">Filas crudas del panel.</param>
    /// <param name="utcNow">Marca de tiempo UTC de referencia.</param>
    public static IReadOnlyList<AssignedPatientSummary> Rank(IEnumerable<AssignedPatientTriageRow> rows, DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(rows);

        return rows
            .Select(row => ToSummary(row, utcNow))
            .OrderByDescending(summary => summary.PriorityLevel)
            .ThenByDescending(summary => summary.LatestIbsSssScore ?? int.MinValue)
            .ThenBy(summary => summary.AssignedAt)
            .ToList();
    }

    /// <summary>
    /// Calcula la prioridad de un paciente. Las evaluaciones IBS-SSS no cuentan como actividad para la media.
    /// </summary>
    /// <param name="row">Fila cruda del panel.</param>
    /// <param name="utcNow">Marca de tiempo UTC de referencia.</param>
    public static PriorityLevel ComputePriority(AssignedPatientTriageRow row, DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (row.LatestIbsSssScore > HighIbsSssScoreThreshold || row.PendingReviewOver24hCount > 0)
        {
            return PriorityLevel.High;
        }

        // Sin registros, la inactividad se cuenta desde el inicio del vínculo.
        var lastRecordAt = MaxDate(row.LastMealAt, row.LastSymptomAt) ?? row.AssignedAt;
        if (lastRecordAt < utcNow.AddHours(-InactivityHours))
        {
            return PriorityLevel.Medium;
        }

        if (row.LatestIbsSssScore is not null || MaxDate(row.LastMealAt, row.LastSymptomAt, row.LastAssessmentAt) is not null)
        {
            return PriorityLevel.Low;
        }

        return PriorityLevel.None;
    }

    private static AssignedPatientSummary ToSummary(AssignedPatientTriageRow row, DateTime utcNow)
    {
        return new AssignedPatientSummary(
            row.PatientUserId,
            row.PatientFullName,
            row.AssignmentId,
            row.AssignedAt,
            row.ProfileCompleted,
            row.IbsSubtype,
            row.LatestIbsSssScore,
            MaxDate(row.LastMealAt, row.LastSymptomAt, row.LastAssessmentAt),
            row.PendingReviewOver24hCount,
            ComputePriority(row, utcNow));
    }

    private static DateTime? MaxDate(params DateTime?[] dates)
    {
        DateTime? max = null;
        foreach (var date in dates)
        {
            if (date is not null && (max is null || date > max))
            {
                max = date;
            }
        }

        return max;
    }
}
