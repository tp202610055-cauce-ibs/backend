using System.Globalization;
using Cauce.Domain.ClinicalRegistry.Enums;
using Cauce.Domain.Identity.Enums;
using Cauce.Domain.Patients.Enums;
using Cauce.Domain.Recommendations.Enums;
using Cauce.Infrastructure.ClinicalRegistry;
using Cauce.Infrastructure.Persistence;
using Cauce.Infrastructure.Qa.Manifest;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Infrastructure.Qa.Inspection;

/// <summary>
/// Secciones que muestra <c>inspect-qa</c>.
/// </summary>
[Flags]
public enum QaInspectSections
{
    /// <summary>
    /// Ninguna sección.
    /// </summary>
    None = 0,

    /// <summary>
    /// Aceptación del consentimiento.
    /// </summary>
    Consent = 1,

    /// <summary>
    /// Comidas recibidas hoy.
    /// </summary>
    Meals = 2,

    /// <summary>
    /// Cuestionarios IBS-SSS respondidos y agendas pendientes.
    /// </summary>
    Ibs = 4,

    /// <summary>
    /// Códigos de invitación de los nutricionistas.
    /// </summary>
    Codes = 8,

    /// <summary>
    /// Recomendaciones y su retroalimentación.
    /// </summary>
    Recs = 16,

    /// <summary>
    /// Estado de la cuenta y datos que conserva.
    /// </summary>
    State = 32,

    /// <summary>
    /// Todas las secciones.
    /// </summary>
    All = Consent | Meals | Ibs | Codes | Recs | State
}

/// <summary>
/// Estado de los datos de un caso o una cuenta para <c>inspect-qa</c>: conteos, estados y fechas, sin textos clínicos
/// ni nombres de alimentos. Solo lee.
/// </summary>
public sealed class QaStateInspector
{
    /// <summary>
    /// Hora de Lima en que el worker del cuestionario envía los avisos del día.
    /// </summary>
    public const int IbsSssNoticeHourLima = 9;

    private readonly CauceDbContext _db;

    /// <summary>
    /// Inicializa el inspector sobre el contexto del ámbito.
    /// </summary>
    public QaStateInspector(CauceDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Líneas de las secciones pedidas, cuenta por cuenta, en el orden del manifiesto.
    /// </summary>
    public async Task<IReadOnlyList<string>> InspectAsync(QaInspectionScope scope, QaInspectSections sections, DateTime nowUtc, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var lines = new List<string>();
        foreach (var account in scope.Accounts)
        {
            var role = account.Role == QaRole.Nutritionist ? "nutricionista" : "paciente";
            if (account.UserId is not { } userId)
            {
                lines.Add($"{account.Email} ({(account.Disposition == QaDisposition.MustNotExist ? "no debe existir" : role)}): no existe en el sistema.");
                continue;
            }

            lines.Add($"{account.Email} ({role}{(account.Shared ? ", compartida con otros casos" : string.Empty)}):");
            if (account.Role == QaRole.Nutritionist)
            {
                if (sections.HasFlag(QaInspectSections.Codes))
                {
                    await AddCodesAsync(scope, userId, nowUtc, lines, ct).ConfigureAwait(false);
                }

                if (sections.HasFlag(QaInspectSections.State))
                {
                    await AddStateAsync(userId, patient: false, lines, ct).ConfigureAwait(false);
                }

                continue;
            }

            if (sections.HasFlag(QaInspectSections.State))
            {
                await AddStateAsync(userId, patient: true, lines, ct).ConfigureAwait(false);
            }

            if (sections.HasFlag(QaInspectSections.Consent))
            {
                await AddConsentAsync(userId, lines, ct).ConfigureAwait(false);
            }

            if (sections.HasFlag(QaInspectSections.Meals))
            {
                await AddMealsAsync(userId, nowUtc, lines, ct).ConfigureAwait(false);
            }

            if (sections.HasFlag(QaInspectSections.Ibs))
            {
                await AddIbsAsync(userId, nowUtc, lines, ct).ConfigureAwait(false);
            }

            if (sections.HasFlag(QaInspectSections.Recs))
            {
                await AddRecommendationsAsync(userId, nowUtc, lines, ct).ConfigureAwait(false);
            }
        }

        return lines;
    }

    private async Task AddStateAsync(Guid userId, bool patient, List<string> lines, CancellationToken ct)
    {
        var user = await _db.Users.AsNoTracking().Where(candidate => candidate.Id == userId)
            .Select(candidate => new { candidate.Status, candidate.Email, candidate.LockedUntil, candidate.IsInActivePilot })
            .SingleAsync(ct).ConfigureAwait(false);
        var anonymized = user.Email.EndsWith("@anonymized.local", StringComparison.Ordinal);
        lines.Add($"  Cuenta: {UserStatusLabel(user.Status)}; correo {(anonymized ? "anonimizado" : "original")}"
            + (patient ? $"; piloto activo {YesNo(user.IsInActivePilot)}" : string.Empty)
            + (user.LockedUntil is not null ? $"; bloqueada hasta {Lima(user.LockedUntil.Value)}" : string.Empty) + ".");
        if (!patient)
        {
            return;
        }

        var meals = await _db.Meals.CountAsync(row => row.PatientId == userId, ct).ConfigureAwait(false);
        var symptoms = await _db.Symptoms.CountAsync(row => row.PatientId == userId, ct).ConfigureAwait(false);
        var assessments = await _db.IbsSssAssessments.CountAsync(row => row.PatientId == userId, ct).ConfigureAwait(false);
        var recommendations = await _db.Recommendations.CountAsync(row => row.PatientId == userId, ct).ConfigureAwait(false);
        var profile = await _db.PatientProfiles.AnyAsync(row => row.UserId == userId, ct).ConfigureAwait(false);
        var links = await _db.NutritionistPatients.CountAsync(row => row.PatientId == userId && row.Status == AssignmentStatus.Active, ct).ConfigureAwait(false);
        lines.Add($"  Datos que conserva: {meals} comida(s), {symptoms} síntoma(s), {assessments} cuestionario(s), {recommendations} recomendación(es); "
            + $"perfil {YesNo(profile)}; {links} vínculo(s) activo(s) con nutricionista.");
    }

    private async Task AddConsentAsync(Guid userId, List<string> lines, CancellationToken ct)
    {
        var consents = await _db.ConsentRecords.AsNoTracking().Where(row => row.UserId == userId).OrderBy(row => row.AcceptedAt)
            .Select(row => new { row.DocumentVersion, row.AcceptedAt, row.IsCurrent }).ToListAsync(ct).ConfigureAwait(false);
        if (consents.Count == 0)
        {
            lines.Add("  Consentimiento: sin aceptación registrada.");
            return;
        }

        foreach (var consent in consents)
        {
            lines.Add($"  Consentimiento: versión {consent.DocumentVersion}, aceptado el {Lima(consent.AcceptedAt)} (Lima), {(consent.IsCurrent ? "vigente" : "reemplazado")}.");
        }
    }

    private async Task AddMealsAsync(Guid userId, DateTime nowUtc, List<string> lines, CancellationToken ct)
    {
        var dayStart = IbsSssScheduleProcessor.EndOfLimaDayUtc(nowUtc).AddDays(-1);
        var meals = await _db.Meals.AsNoTracking()
            .Where(row => row.PatientId == userId && row.CreatedAt >= dayStart)
            .OrderBy(row => row.CreatedAt)
            .Select(row => new { row.CreatedAt, row.ConsumedAt, row.ClientCreatedAt, row.MealTime, row.SyncStatus })
            .ToListAsync(ct).ConfigureAwait(false);
        lines.Add($"  Comidas que llegaron al servidor hoy (día de Lima): {meals.Count}.");
        foreach (var meal in meals)
        {
            lines.Add($"    llegó {Lima(meal.CreatedAt)}; registrada en el teléfono {Lima(meal.ClientCreatedAt)}; consumida {Lima(meal.ConsumedAt)}; "
                + $"{MealTimeLabel(meal.MealTime)}; {(meal.SyncStatus == SyncStatus.SyncCompleted ? "sincronizada" : "pendiente de sincronizar")}.");
        }
    }

    private async Task AddIbsAsync(Guid userId, DateTime nowUtc, List<string> lines, CancellationToken ct)
    {
        var answered = await _db.IbsSssAssessments.AsNoTracking().Where(row => row.PatientId == userId)
            .OrderByDescending(row => row.CompletedAt).Select(row => (DateTime?)row.CompletedAt).ToListAsync(ct).ConfigureAwait(false);
        lines.Add($"  Cuestionarios respondidos: {answered.Count}{(answered.Count > 0 ? $"; el último, el {Lima(answered[0]!.Value)} (Lima)" : string.Empty)}.");

        var schedules = await _db.IbsSssSchedules.AsNoTracking().Where(row => row.PatientId == userId && !row.Completed)
            .OrderBy(row => row.DueDate).ToListAsync(ct).ConfigureAwait(false);
        var today = DateOnly.FromDateTime(nowUtc + QaClock.LimaOffset);
        foreach (var schedule in schedules)
        {
            var due = DateOnly.FromDateTime(schedule.DueDate + QaClock.LimaOffset);
            var notice = await _db.Notifications.AsNoTracking()
                .Where(row => row.RelatedEntityType == IbsSssScheduleProcessor.RelatedEntityType && row.RelatedEntityId == schedule.Id)
                .OrderBy(row => row.ScheduledFor).Select(row => (DateTime?)row.ScheduledFor).FirstOrDefaultAsync(ct).ConfigureAwait(false);
            var when = due == today ? "le toca hoy" : due < today ? "vencido" : "próximo";
            lines.Add($"  Cuestionario pendiente: {when}, el {due:yyyy-MM-dd}; "
                + (schedule.Missed ? "marcado como no registrado; " : string.Empty)
                + $"aviso {(notice is null ? "aún no enviado" : $"programado el {Lima(notice.Value)}")}; "
                + $"recordatorio de 48 h {(schedule.ReminderSentAt is null ? "no enviado" : $"enviado el {Lima(schedule.ReminderSentAt.Value)}")}.");
        }

        lines.Add($"  Hora del aviso del cuestionario: {IbsSssNoticeHourLima:00}:00 de Lima, todos los días; en QA lo dispara el operador con process-ibs-sss.");
    }

    private async Task AddRecommendationsAsync(Guid userId, DateTime nowUtc, List<string> lines, CancellationToken ct)
    {
        var recommendations = await _db.Recommendations.AsNoTracking().Where(row => row.PatientId == userId)
            .OrderBy(row => row.GeneratedAt).ToListAsync(ct).ConfigureAwait(false);
        lines.Add($"  Recomendaciones: {recommendations.Count}.");
        foreach (var recommendation in recommendations)
        {
            var feedback = await _db.RecommendationFeedback.AsNoTracking().Where(row => row.RecommendationId == recommendation.Id)
                .OrderBy(row => row.SubmittedAt).Select(row => new { row.WasApplied, row.Outcome, row.SubmittedAt })
                .ToListAsync(ct).ConfigureAwait(false);
            var state = recommendation.IsExpired(nowUtc) ? $"{StatusLabel(recommendation.Status)} con la ventana vencida (se lee como vencida)" : StatusLabel(recommendation.Status);
            var archive = recommendation.IsActive
                ? "activa"
                : $"archivada el {Lima(recommendation.ArchivedAt ?? nowUtc)} ({ArchiveLabel(recommendation.ArchiveReason)})";
            var validity = recommendation.ValidUntil is { } validUntil
                ? $"; vigencia hasta {Lima(validUntil)}{(validUntil < nowUtc && recommendation.IsActive ? ", vencida y pendiente de archivo" : string.Empty)}"
                : string.Empty;
            var answers = feedback.Count == 0
                ? "sin respuesta del paciente"
                : string.Join(
                    "; ",
                    feedback.Select(row => $"respuesta del {Lima(row.SubmittedAt)}: la aplicó {YesNo(row.WasApplied)}, resultado {OutcomeLabel(row.Outcome)}"));
            lines.Add($"    #{recommendation.Id.ToString("N")[..8]} {(recommendation.ExplanationSource == ExplanationSource.Manual ? "manual" : "del motor")}: "
                + $"{state}; {archive}{validity}; {answers}.");
        }
    }

    private async Task AddCodesAsync(QaInspectionScope scope, Guid nutritionistId, DateTime nowUtc, List<string> lines, CancellationToken ct)
    {
        var codes = await _db.InvitationCodes.AsNoTracking().Where(row => row.NutritionistId == nutritionistId)
            .OrderBy(row => row.CreatedAt).ToListAsync(ct).ConfigureAwait(false);
        lines.Add($"  Códigos de invitación: {codes.Count}.");
        foreach (var code in codes)
        {
            var state = code.Status switch
            {
                InvitationStatus.Used => $"canjeado el {Lima(code.UsedAt ?? code.CreatedAt)} por {scope.EmailOf(code.UsedByPatientId) ?? "otra cuenta"}",
                InvitationStatus.Expired => "vencido",
                _ when !code.IsValid(nowUtc) => "vencido (sin canjear)",
                _ => "vigente"
            };
            lines.Add($"    {Mask(code.Code)}: {state}; creado el {Lima(code.CreatedAt)}; vence el {Lima(code.ExpiresAt)}.");
        }
    }

    // El código es un secreto de un solo uso: se muestran sus extremos, que bastan para reconocerlo en el portal.
    private static string Mask(string code) => code.Length <= 4 ? "****" : $"{code[..2]}{new string('*', code.Length - 4)}{code[^2..]}";

    private static string Lima(DateTime utc) => (utc + QaClock.LimaOffset).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    private static string YesNo(bool value) => value ? "sí" : "no";

    private static string UserStatusLabel(UserStatus status) => status switch
    {
        UserStatus.Active => "activa",
        UserStatus.PendingActivation => "pendiente de activación",
        UserStatus.Inactive => "inactiva",
        UserStatus.Suspended => "suspendida",
        _ => status.ToString()
    };

    private static string StatusLabel(RecommendationStatus status) => status switch
    {
        RecommendationStatus.Generated => "generada",
        RecommendationStatus.PendingReview => "pendiente de revisión",
        RecommendationStatus.Approved => "aprobada",
        RecommendationStatus.ModifiedApproved => "aprobada con cambios",
        RecommendationStatus.ManualApproved => "creada por el nutricionista",
        RecommendationStatus.Rejected => "rechazada",
        RecommendationStatus.Delivered => "entregada",
        RecommendationStatus.FeedbackReceived => "con respuesta del paciente",
        RecommendationStatus.Expired => "vencida",
        _ => status.ToString()
    };

    private static string ArchiveLabel(ArchiveReason? reason) => reason switch
    {
        ArchiveReason.TemporalExpiration => "por vigencia vencida",
        ArchiveReason.ManualSubstitution => "reemplazada por otra",
        ArchiveReason.ObjectiveMet => "objetivo cumplido",
        ArchiveReason.PlanChange => "cambio de plan",
        _ => "sin motivo"
    };

    private static string OutcomeLabel(FeedbackOutcome outcome) => outcome switch
    {
        FeedbackOutcome.Improvement => "mejoró",
        FeedbackOutcome.NoChange => "sin cambios",
        FeedbackOutcome.Worsening => "empeoró",
        _ => outcome.ToString()
    };

    private static string MealTimeLabel(MealTime mealTime) => mealTime switch
    {
        MealTime.Breakfast => "desayuno",
        MealTime.Lunch => "almuerzo",
        MealTime.Dinner => "cena",
        MealTime.Snack => "refrigerio",
        _ => mealTime.ToString()
    };
}
