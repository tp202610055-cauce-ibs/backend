using System.Text.Json;
using Cauce.Application.Common.Auditing;
using Cauce.Domain.Auditing.Enums;
using Cauce.Infrastructure.Persistence;
using Cauce.Infrastructure.Qa.Manifest;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Infrastructure.Qa.Inspection;

/// <summary>
/// Cómo se relaciona una fila de auditoría con el alcance inspeccionado.
/// </summary>
public enum QaAuditMatch
{
    /// <summary>
    /// La hizo una cuenta del alcance o toca sus datos.
    /// </summary>
    Account,

    /// <summary>
    /// Intento de ingreso sin actor: el correo no tiene cuenta y no se puede atribuir.
    /// </summary>
    UnknownEmail,

    /// <summary>
    /// Solicitud anónima cuyo correo enmascarado coincide con una cuenta del alcance.
    /// </summary>
    MaskedEmail
}

/// <summary>
/// Fila de auditoría lista para mostrar. No lleva IP, user agent ni hashes: no se leen de la base.
/// </summary>
public sealed record QaAuditRow(
    DateTime OccurredAt, AuditActionType Action, string EntityType, Guid? EntityId, Guid? ActorUserId, string? Context, QaAuditMatch Match);

/// <summary>
/// Lectura de <c>audit_logs</c> para <c>audit-log</c>: las filas de las cuentas de un caso o de una cuenta, sin escribir.
/// </summary>
public sealed class QaAuditLogReader
{
    private static readonly AuditActionType[] SessionActions =
    [
        AuditActionType.Login,
        AuditActionType.Logout,
        AuditActionType.FailedLogin,
        AuditActionType.FailedLogout,
        AuditActionType.AccountLocked,
        AuditActionType.AccountActivation
    ];

    private static readonly AuditActionType[] AnonymousLoginActions = [AuditActionType.FailedLogin, AuditActionType.AccountLocked];

    private static readonly AuditActionType[] MaskedEmailActions =
        [AuditActionType.PasswordResetRequest, AuditActionType.VerificationEmailResendRequest];

    // Acciones cuyo paciente solo figura en el contexto adicional.
    private static readonly AuditActionType[] PatientInContextActions = [AuditActionType.ExportPdf, AuditActionType.NutritionistAssignment];

    private readonly CauceDbContext _db;

    /// <summary>
    /// Inicializa el lector sobre el contexto del ámbito.
    /// </summary>
    public QaAuditLogReader(CauceDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Filas desde <paramref name="sinceUtc"/>, de la más antigua a la más reciente, con las <paramref name="limit"/>
    /// más recientes. Una cuenta compartida aporta sus ingresos y lo que hizo sobre los datos del alcance.
    /// </summary>
    public async Task<IReadOnlyList<QaAuditRow>> ReadAsync(
        QaInspectionScope scope, DateTime sinceUtc, IReadOnlyCollection<AuditActionType> actions, int limit, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(actions);
        var accountIds = scope.Accounts.Where(account => account.UserId is not null).Select(account => account.UserId!.Value).ToList();
        var exclusiveIds = scope.Accounts.Where(account => account is { Shared: false, UserId: not null }).Select(account => account.UserId!.Value).ToHashSet();
        var ownedIds = await OwnedEntityIdsAsync(scope, accountIds, ct).ConfigureAwait(false);
        var ownedList = ownedIds.ToList();
        var anonymousLogins = scope.HasMustNotExist;
        var masks = scope.Accounts.Select(account => AuditMask.Email(account.Email)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var patientIds = scope.PatientIds.Select(id => id.ToString()).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var candidates = await _db.AuditLogs.AsNoTracking()
            .Where(log => log.OccurredAt >= sinceUtc)
            .Where(log => (log.ActorUserId != null && accountIds.Contains(log.ActorUserId.Value))
                || (log.EntityId != null && ownedList.Contains(log.EntityId.Value))
                || (log.ActorUserId == null && (AnonymousLoginActions.Contains(log.ActionType) || MaskedEmailActions.Contains(log.ActionType)))
                || PatientInContextActions.Contains(log.ActionType))
            .OrderByDescending(log => log.OccurredAt)
            .Select(log => new { log.OccurredAt, log.ActionType, log.EntityType, log.EntityId, log.ActorUserId, log.AdditionalContext })
            .ToListAsync(ct).ConfigureAwait(false);

        var rows = new List<QaAuditRow>();
        foreach (var log in candidates)
        {
            if (actions.Count > 0 && !actions.Contains(log.ActionType))
            {
                continue;
            }

            QaAuditMatch? match = null;
            var owned = log.EntityId is not null && ownedIds.Contains(log.EntityId.Value);
            if (log.ActorUserId is { } actor && accountIds.Contains(actor))
            {
                if (exclusiveIds.Contains(actor) || owned || SessionActions.Contains(log.ActionType) || PatientInContext(log.AdditionalContext, patientIds))
                {
                    match = QaAuditMatch.Account;
                }
            }
            else if (owned || (PatientInContextActions.Contains(log.ActionType) && PatientInContext(log.AdditionalContext, patientIds)))
            {
                match = QaAuditMatch.Account;
            }
            else if (log.ActorUserId is null && anonymousLogins && AnonymousLoginActions.Contains(log.ActionType))
            {
                match = QaAuditMatch.UnknownEmail;
            }
            else if (log.ActorUserId is null && MaskedEmailActions.Contains(log.ActionType) && ContextString(log.AdditionalContext, "email") is { } mask && masks.Contains(mask))
            {
                match = QaAuditMatch.MaskedEmail;
            }

            if (match is not null)
            {
                rows.Add(new QaAuditRow(log.OccurredAt, log.ActionType, log.EntityType, log.EntityId, log.ActorUserId, log.AdditionalContext, match.Value));
                if (rows.Count == limit)
                {
                    break;
                }
            }
        }

        rows.Reverse();
        return rows;
    }

    // Identificadores de los registros de los pacientes del alcance y de sus cuentas, los que usan los triggers.
    private async Task<HashSet<Guid>> OwnedEntityIdsAsync(QaInspectionScope scope, IReadOnlyList<Guid> accountIds, CancellationToken ct)
    {
        var patients = scope.PatientIds.ToList();
        var ids = new HashSet<Guid>(accountIds);
        var recommendations = await _db.Recommendations.AsNoTracking().Where(row => patients.Contains(row.PatientId)).Select(row => row.Id).ToListAsync(ct).ConfigureAwait(false);
        ids.UnionWith(recommendations);
        ids.UnionWith(await _db.RecommendationFeedback.AsNoTracking().Where(row => recommendations.Contains(row.RecommendationId)).Select(row => row.Id).ToListAsync(ct).ConfigureAwait(false));
        ids.UnionWith(await _db.Meals.AsNoTracking().Where(row => patients.Contains(row.PatientId)).Select(row => row.Id).ToListAsync(ct).ConfigureAwait(false));
        ids.UnionWith(await _db.Symptoms.AsNoTracking().Where(row => patients.Contains(row.PatientId)).Select(row => row.Id).ToListAsync(ct).ConfigureAwait(false));
        ids.UnionWith(await _db.IbsSssAssessments.AsNoTracking().Where(row => patients.Contains(row.PatientId)).Select(row => row.Id).ToListAsync(ct).ConfigureAwait(false));
        ids.UnionWith(await _db.PatientProfiles.AsNoTracking().Where(row => patients.Contains(row.UserId)).Select(row => row.Id).ToListAsync(ct).ConfigureAwait(false));
        ids.UnionWith(await _db.PatientAllergies.AsNoTracking().Where(row => patients.Contains(row.PatientId)).Select(row => row.Id).ToListAsync(ct).ConfigureAwait(false));
        ids.UnionWith(await _db.NutritionistPatients.AsNoTracking().Where(row => patients.Contains(row.PatientId)).Select(row => row.Id).ToListAsync(ct).ConfigureAwait(false));
        ids.UnionWith(await _db.CustomFoods.AsNoTracking().Where(row => patients.Contains(row.PatientId)).Select(row => row.Id).ToListAsync(ct).ConfigureAwait(false));
        ids.UnionWith(await _db.ClinicalNotes.AsNoTracking().Where(row => patients.Contains(row.PatientId)).Select(row => row.Id).ToListAsync(ct).ConfigureAwait(false));

        var nutritionists = scope.Accounts.Where(account => account is { Role: QaRole.Nutritionist, Shared: false, UserId: not null })
            .Select(account => account.UserId!.Value).ToList();
        ids.UnionWith(await _db.InvitationCodes.AsNoTracking().Where(row => nutritionists.Contains(row.NutritionistId)).Select(row => row.Id).ToListAsync(ct).ConfigureAwait(false));
        return ids;
    }

    private static bool PatientInContext(string? context, IReadOnlySet<string> patientIds) =>
        (ContextString(context, "patient_id") ?? ContextString(context, "patientId")) is { } patient && patientIds.Contains(patient);

    private static string? ContextString(string? context, string key)
    {
        if (string.IsNullOrWhiteSpace(context))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(context);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty(key, out var value)
                && value.ValueKind == JsonValueKind.String
                    ? value.GetString()
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
