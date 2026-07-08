using System.Globalization;
using System.IO.Compression;
using System.Text;
using Cauce.Domain.Auditing;
using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.Identity;
using Cauce.Domain.Patients;
using Cauce.Domain.Recommendations;

namespace Cauce.Infrastructure.Patients;

/// <summary>
/// Conjunto de datos de un paciente que alimenta la exportación de portabilidad (US25). Sus colecciones
/// se materializan desde la base de datos y se pasan al constructor del archivo, que es una función pura
/// (sin E/S) para poder probar el formato de los CSV sin infraestructura.
/// </summary>
/// <param name="Profile">Perfil clínico del paciente, o <see langword="null"/> si no existe.</param>
/// <param name="Allergies">Declaraciones de alergia del paciente.</param>
/// <param name="Meals">Comidas registradas por el paciente.</param>
/// <param name="Symptoms">Síntomas reportados por el paciente.</param>
/// <param name="Assessments">Evaluaciones IBS-SSS del paciente.</param>
/// <param name="Recommendations">Recomendaciones dietéticas del paciente.</param>
/// <param name="Feedback">Retroalimentaciones del paciente sobre sus recomendaciones.</param>
/// <param name="ConsentRecords">Registros de consentimiento del paciente.</param>
/// <param name="AuditLogs">Entradas de auditoría en las que el paciente fue el actor.</param>
public sealed record ClinicalDataSet(
    PatientProfile? Profile,
    IReadOnlyList<PatientAllergy> Allergies,
    IReadOnlyList<Meal> Meals,
    IReadOnlyList<Symptom> Symptoms,
    IReadOnlyList<IbsSssAssessment> Assessments,
    IReadOnlyList<Recommendation> Recommendations,
    IReadOnlyList<RecommendationFeedback> Feedback,
    IReadOnlyList<ConsentRecord> ConsentRecords,
    IReadOnlyList<AuditLog> AuditLogs);

/// <summary>
/// Resultado de la construcción del archivo de exportación.
/// </summary>
/// <param name="ZipContent">Contenido binario del archivo ZIP.</param>
/// <param name="Counts">Conteo de filas por entidad (nombre de CSV → número de filas).</param>
public sealed record ClinicalDataArchive(byte[] ZipContent, IReadOnlyDictionary<string, int> Counts);

/// <summary>
/// Construye el archivo ZIP de exportación de datos del paciente a partir de un
/// <see cref="ClinicalDataSet"/>. Cada entidad se serializa como un CSV independiente; los
/// encabezados están siempre presentes aunque no haya filas (US25 CA02).
/// </summary>
public static class ClinicalDataArchiveBuilder
{
    /// <summary>
    /// Construye el archivo ZIP con un CSV por entidad.
    /// </summary>
    /// <param name="data">Conjunto de datos del paciente.</param>
    /// <returns>El contenido del ZIP y el conteo de filas por entidad.</returns>
    public static ClinicalDataArchive Build(ClinicalDataSet data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        using var buffer = new MemoryStream();

        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            var profileRows = data.Profile is null
                ? Array.Empty<string[]>()
                : new[] { ProfileRow(data.Profile) };
            WriteCsv(archive, "profile.csv", counts,
                new[] { "user_id", "date_of_birth", "biological_sex", "weight_kg", "height_cm", "ibs_subtype", "diagnosis_date", "medications", "onboarding_completed", "created_at", "updated_at" },
                profileRows);

            WriteCsv(archive, "allergies.csv", counts,
                new[] { "patient_allergy_id", "allergy_id", "severity", "notes", "declared_at" },
                data.Allergies.Select(a => new[]
                {
                    a.Id.ToString(), a.AllergyId.ToString(), a.Severity.ToString(), Text(a.Notes), Utc(a.DeclaredAt)
                }));

            WriteCsv(archive, "meals.csv", counts,
                new[] { "meal_id", "client_guid", "meal_time", "consumed_at", "item_count", "sync_status", "created_at", "client_created_at" },
                data.Meals.Select(m => new[]
                {
                    m.Id.ToString(), m.ClientGuid.ToString(), m.MealTime.ToString(), Utc(m.ConsumedAt),
                    m.GetItemCount().ToString(CultureInfo.InvariantCulture), m.SyncStatus.ToString(), Utc(m.CreatedAt), Utc(m.ClientCreatedAt)
                }));

            WriteCsv(archive, "symptoms.csv", counts,
                new[] { "symptom_id", "client_guid", "symptom_type", "intensity", "occurred_at", "associated_meal_id", "has_meal_association", "sync_status", "created_at", "client_created_at" },
                data.Symptoms.Select(s => new[]
                {
                    s.Id.ToString(), s.ClientGuid.ToString(), s.SymptomType.ToString(), Num(s.Intensity), Utc(s.OccurredAt),
                    s.AssociatedMealId?.ToString() ?? string.Empty, s.HasMealAssociation.ToString(), s.SyncStatus.ToString(), Utc(s.CreatedAt), Utc(s.ClientCreatedAt)
                }));

            WriteCsv(archive, "ibs_sss_assessments.csv", counts,
                new[] { "assessment_id", "assessment_type", "cycle_number", "pain_severity", "pain_frequency", "bloating_severity", "bowel_habits_dissatisfaction", "life_interference", "total_score", "severity_category", "completed_at", "next_assessment_date" },
                data.Assessments.Select(a => new[]
                {
                    a.Id.ToString(), a.AssessmentType.ToString(), Num(a.CycleNumber), Num(a.PainSeverity), Num(a.PainFrequency),
                    Num(a.BloatingSeverity), Num(a.BowelHabitsDissatisfaction), Num(a.LifeInterference), Num(a.TotalScore),
                    a.SeverityCategory.ToString(), Utc(a.CompletedAt), a.NextAssessmentDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty
                }));

            WriteCsv(archive, "recommendations.csv", counts,
                new[] { "recommendation_id", "model_version_id", "status", "confidence_score", "auto_approved", "explanation_source", "ai_explanation", "nutritionist_note", "generated_at", "reviewed_at", "delivered_at", "expires_at" },
                data.Recommendations.Select(r => new[]
                {
                    r.Id.ToString(), r.ModelVersionId?.ToString() ?? string.Empty, r.Status.ToString(),
                    r.ConfidenceScore.Value.ToString(CultureInfo.InvariantCulture), r.AutoApproved.ToString(), r.ExplanationSource.ToString(),
                    Text(r.AiExplanation), Text(r.NutritionistNote), Utc(r.GeneratedAt), UtcOrEmpty(r.ReviewedAt), UtcOrEmpty(r.DeliveredAt), UtcOrEmpty(r.ExpiresAt)
                }));

            WriteCsv(archive, "recommendation_feedback.csv", counts,
                new[] { "feedback_id", "recommendation_id", "was_applied", "outcome", "comment", "submitted_at" },
                data.Feedback.Select(f => new[]
                {
                    f.Id.ToString(), f.RecommendationId.ToString(), f.WasApplied.ToString(), f.Outcome.ToString(), Text(f.Comment), Utc(f.SubmittedAt)
                }));

            WriteCsv(archive, "consent_records.csv", counts,
                new[] { "consent_id", "document_version", "accepted_at", "ip_address", "consent_text_hash", "is_current" },
                data.ConsentRecords.Select(c => new[]
                {
                    c.Id.ToString(), Text(c.DocumentVersion), Utc(c.AcceptedAt), Text(c.IpAddress), Text(c.ConsentTextHash), c.IsCurrent.ToString()
                }));

            WriteCsv(archive, "audit_logs.csv", counts,
                new[] { "audit_log_id", "action_type", "entity_type", "entity_id", "occurred_at", "ip_address", "additional_context" },
                data.AuditLogs.Select(l => new[]
                {
                    l.Id.ToString(), l.ActionType.ToString(), Text(l.EntityType), l.EntityId?.ToString() ?? string.Empty,
                    Utc(l.OccurredAt), Text(l.IpAddress), Text(l.AdditionalContext)
                }));
        }

        return new ClinicalDataArchive(buffer.ToArray(), counts);
    }

    private static string[] ProfileRow(PatientProfile profile) =>
    [
        profile.UserId.ToString(),
        profile.DateOfBirth.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        profile.BiologicalSex.ToString(),
        profile.WeightKg.ToString(CultureInfo.InvariantCulture),
        profile.HeightCm.ToString(CultureInfo.InvariantCulture),
        profile.IbsSubtype.ToString(),
        profile.DiagnosisDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty,
        Text(profile.Medications),
        profile.OnboardingCompleted.ToString(),
        Utc(profile.CreatedAt),
        Utc(profile.UpdatedAt)
    ];

    private static void WriteCsv(
        ZipArchive archive,
        string entryName,
        Dictionary<string, int> counts,
        string[] headers,
        IEnumerable<string[]> rows)
    {
        var materialized = rows as IReadOnlyList<string[]> ?? rows.ToList();
        counts[entryName] = materialized.Count;

        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        writer.WriteLine(string.Join(',', headers.Select(Escape)));
        foreach (var row in materialized)
        {
            writer.WriteLine(string.Join(',', row.Select(Escape)));
        }
    }

    private static string Escape(string? field)
    {
        var value = field ?? string.Empty;
        if (value.IndexOfAny(['"', ',', '\n', '\r']) < 0)
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }

    private static string Text(string? value) => value ?? string.Empty;

    private static string Num(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Utc(DateTime value) => value.ToString("O", CultureInfo.InvariantCulture);

    private static string UtcOrEmpty(DateTime? value) => value is null ? string.Empty : Utc(value.Value);
}
