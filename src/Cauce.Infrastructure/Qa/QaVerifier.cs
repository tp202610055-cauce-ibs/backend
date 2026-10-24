using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Domain.Identity;
using Cauce.Domain.Identity.Enums;
using Cauce.Domain.Patients.Enums;
using Cauce.Domain.Recommendations.Enums;
using Cauce.Infrastructure.Persistence;
using Cauce.Infrastructure.Qa.Manifest;
using Cauce.Infrastructure.Qa.Seeding;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Infrastructure.Qa;

/// <summary>
/// Resultado de verificar un caso: sin motivos, el caso está listo.
/// </summary>
public sealed record QaCaseVerdict(string Cp, IReadOnlyList<string> Problems)
{
    /// <summary>
    /// Indica si el caso pasó todas las comprobaciones.
    /// </summary>
    public bool Ok => Problems.Count == 0;
}

/// <summary>
/// Verificación de solo lectura (<c>verify-qa</c>): cuentas y su estado, cuentas que no deben existir, vínculos,
/// perfil y alergias, recomendaciones por estado, códigos, agendas y la edad del ancla de los casos con reloj.
/// Nunca escribe: consultas sin seguimiento y solo lecturas en Keycloak.
/// </summary>
public sealed class QaVerifier
{
    /// <summary>
    /// Horas que puede pasar desde el rearme de un caso con reloj en horas antes de darlo por vencido.
    /// </summary>
    public const int HoursClockToleranceHours = 2;

    private readonly CauceDbContext _db;
    private readonly IKeycloakAdminClient _keycloak;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Inicializa el verificador con sus dependencias.
    /// </summary>
    public QaVerifier(CauceDbContext db, IKeycloakAdminClient keycloak, TimeProvider timeProvider)
    {
        _db = db;
        _keycloak = keycloak;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Verifica los casos indicados, en el orden del manifiesto.
    /// </summary>
    public async Task<IReadOnlyList<QaCaseVerdict>> VerifyAsync(QaManifest manifest, IReadOnlyList<QaCase> cases, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(cases);
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var verdicts = new List<QaCaseVerdict>();
        foreach (var testCase in cases)
        {
            var problems = new List<string>();
            await CheckAccountsAsync(manifest, testCase, problems, ct).ConfigureAwait(false);
            await CheckSeedsAsync(manifest, testCase, now, problems, ct).ConfigureAwait(false);
            await CheckClockAsync(manifest, testCase, now, problems, ct).ConfigureAwait(false);
            verdicts.Add(new QaCaseVerdict(testCase.Cp, problems));
        }

        return verdicts;
    }

    /// <summary>
    /// Estado final que debe tener una recomendación sembrada, según lo que el paciente hizo con ella.
    /// </summary>
    public static RecommendationStatus ExpectedStatus(QaRecommendationSeed seed)
    {
        ArgumentNullException.ThrowIfNull(seed);
        var approved = seed.Status switch
        {
            QaRecommendationStatus.PendingReview => RecommendationStatus.PendingReview,
            QaRecommendationStatus.Rejected => RecommendationStatus.Rejected,
            QaRecommendationStatus.Expired => RecommendationStatus.Expired,
            QaRecommendationStatus.ApprovedModified => RecommendationStatus.ModifiedApproved,
            QaRecommendationStatus.CreatedByNutritionist => RecommendationStatus.ManualApproved,
            _ => RecommendationStatus.Approved
        };

        if (approved is RecommendationStatus.PendingReview or RecommendationStatus.Rejected or RecommendationStatus.Expired)
        {
            return approved;
        }

        return seed.PatientResponded ? RecommendationStatus.FeedbackReceived
            : seed.PatientOpened ? RecommendationStatus.Delivered
            : approved;
    }

    private async Task CheckAccountsAsync(QaManifest manifest, QaCase testCase, List<string> problems, CancellationToken ct)
    {
        foreach (var account in testCase.ReferencedEmails().Select(manifest.FindAccount).OfType<QaAccount>())
        {
            var email = EmailNormalization.Normalize(account.Email);
            var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Email == email, ct).ConfigureAwait(false);
            var identity = await _keycloak.FindByEmailAsync(email, ct).ConfigureAwait(false);

            if (account.Disposition == QaDisposition.MustNotExist)
            {
                if (user is not null || identity is not null)
                {
                    problems.Add($"{account.Email} existe y no debe existir");
                }

                continue;
            }

            if (user is null || identity is null)
            {
                problems.Add($"{account.Email} no existe{(user is null ? string.Empty : " en Keycloak")}");
                continue;
            }

            var status = account.Status ?? QaAccountStatus.Active;
            var expected = status switch
            {
                QaAccountStatus.Disabled => UserStatus.Suspended,
                QaAccountStatus.PendingActivation => UserStatus.PendingActivation,
                _ => UserStatus.Active
            };
            if (user.Status != expected)
            {
                problems.Add($"{account.Email} está {user.Status} y debe estar {expected}");
            }

            if (user.LockedUntil is not null && user.LockedUntil > _timeProvider.GetUtcNow().UtcDateTime)
            {
                problems.Add($"{account.Email} está bloqueada");
            }

            var state = await _keycloak.GetUserStateAsync(identity.Id, ct).ConfigureAwait(false);
            if (state.Enabled != (status != QaAccountStatus.Disabled))
            {
                problems.Add($"{account.Email} tiene Keycloak {(state.Enabled ? "habilitado" : "deshabilitado")}");
            }

            if (status != QaAccountStatus.PendingActivation && state.RequiredActions.Count > 0)
            {
                problems.Add($"{account.Email} tiene acciones pendientes en Keycloak");
            }
        }
    }

    private async Task CheckSeedsAsync(QaManifest manifest, QaCase testCase, DateTime now, List<string> problems, CancellationToken ct)
    {
        foreach (var seed in testCase.Seeds)
        {
            switch (seed)
            {
                case QaLinkSeed link:
                    var patientId = await UserIdAsync(link.Patient, ct).ConfigureAwait(false);
                    var nutritionistId = await UserIdAsync(link.Nutritionist, ct).ConfigureAwait(false);
                    if (!await _db.NutritionistPatients.AsNoTracking().AnyAsync(
                            candidate => candidate.PatientId == patientId && candidate.NutritionistId == nutritionistId && candidate.Status == AssignmentStatus.Active,
                            ct).ConfigureAwait(false))
                    {
                        problems.Add($"falta el vínculo de {link.Patient} con {link.Nutritionist}");
                    }

                    break;
                case QaProfileSeed profile:
                    await CheckProfileAsync(profile, problems, ct).ConfigureAwait(false);
                    break;
                case QaIbsDueSeed or QaIbsInvitationSeed { Answered: false }:
                    var schedulePatient = await UserIdAsync(seed.ReferencedEmails().First(), ct).ConfigureAwait(false);
                    if (!await _db.IbsSssSchedules.AsNoTracking().AnyAsync(
                            schedule => schedule.PatientId == schedulePatient && !schedule.Completed && !schedule.Missed, ct).ConfigureAwait(false))
                    {
                        problems.Add($"{seed.ReferencedEmails().First()} no tiene el cuestionario pendiente");
                    }

                    break;
            }
        }

        foreach (var patient in testCase.Seeds.OfType<QaRecommendationSeed>().Select(seed => seed.Patient).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            await CheckRecommendationsAsync(manifest, patient, now, problems, ct).ConfigureAwait(false);
        }

        foreach (var nutritionist in testCase.Seeds.OfType<QaInviteCodeSeed>().Select(seed => seed.Nutritionist).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            await CheckInviteCodesAsync(manifest, nutritionist, now, problems, ct).ConfigureAwait(false);
        }
    }

    private async Task CheckProfileAsync(QaProfileSeed seed, List<string> problems, CancellationToken ct)
    {
        var patientId = await UserIdAsync(seed.Patient, ct).ConfigureAwait(false);
        var profile = await _db.PatientProfiles.AsNoTracking().FirstOrDefaultAsync(candidate => candidate.UserId == patientId, ct).ConfigureAwait(false);
        if (!seed.Complete)
        {
            if (profile is not null)
            {
                problems.Add($"{seed.Patient} ya tiene perfil y no debe tenerlo");
            }

            return;
        }

        if (profile is not { OnboardingCompleted: true })
        {
            problems.Add($"{seed.Patient} no tiene el perfil completo");
            return;
        }

        var allergies = await _db.PatientAllergies.AsNoTracking()
            .Where(declared => declared.PatientId == patientId)
            .Join(_db.Allergies, declared => declared.AllergyId, allergy => allergy.Id, (_, allergy) => allergy.Name)
            .ToListAsync(ct).ConfigureAwait(false);
        if (!allergies.Order(StringComparer.OrdinalIgnoreCase).SequenceEqual(seed.Allergies.Order(StringComparer.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase))
        {
            problems.Add($"{seed.Patient} tiene {allergies.Count} alergia(s) y debe tener {seed.Allergies.Count}");
        }
    }

    private async Task CheckRecommendationsAsync(QaManifest manifest, string patient, DateTime now, List<string> problems, CancellationToken ct)
    {
        var expected = QaSeedOwnership.OwnedBy(manifest, patient)
            .Select(pair => pair.Seed)
            .OfType<QaRecommendationSeed>()
            .GroupBy(ExpectedStatus)
            .ToDictionary(group => group.Key, group => group.Count());
        var patientId = await UserIdAsync(patient, ct).ConfigureAwait(false);
        var actual = await _db.Recommendations.AsNoTracking()
            .Where(recommendation => recommendation.PatientId == patientId)
            .ToListAsync(ct).ConfigureAwait(false);

        // La expiración también se evalúa al leer: una pendiente con la ventana vencida ya cuenta como expirada.
        var counts = actual
            .GroupBy(recommendation => recommendation.IsExpired(now) ? RecommendationStatus.Expired : recommendation.Status)
            .ToDictionary(group => group.Key, group => group.Count());
        foreach (var status in expected.Keys.Union(counts.Keys))
        {
            var want = expected.GetValueOrDefault(status);
            var have = counts.GetValueOrDefault(status);
            if (want != have)
            {
                problems.Add($"{patient} tiene {have} recomendación(es) {status} y debe tener {want}");
            }
        }
    }

    private async Task CheckInviteCodesAsync(QaManifest manifest, string nutritionist, DateTime now, List<string> problems, CancellationToken ct)
    {
        var nutritionistId = await UserIdAsync(nutritionist, ct).ConfigureAwait(false);
        var codes = await _db.InvitationCodes.AsNoTracking()
            .Where(code => code.NutritionistId == nutritionistId)
            .ToListAsync(ct).ConfigureAwait(false);
        var expected = QaSeedOwnership.OwnedBy(manifest, nutritionist).Select(pair => pair.Seed).OfType<QaInviteCodeSeed>().ToList();

        var valid = codes.Count(code => code.IsValid(now));
        var expired = codes.Count(code => code.Status == InvitationStatus.Expired || (code.Status == InvitationStatus.Active && !code.IsValid(now)));
        var used = codes.Count(code => code.Status == InvitationStatus.Used);
        if (valid < expected.Count(seed => seed.State == QaInviteCodeState.Valid)
            || expired < expected.Count(seed => seed.State == QaInviteCodeState.Expired)
            || used < expected.Count(seed => seed.State == QaInviteCodeState.Used))
        {
            problems.Add($"{nutritionist} tiene {valid} código(s) vigente(s), {expired} vencido(s) y {used} canjeado(s)");
        }
    }

    // El consentimiento de las cuentas exclusivas se fecha en el ancla de su última siembra.
    private async Task CheckClockAsync(QaManifest manifest, QaCase testCase, DateTime now, List<string> problems, CancellationToken ct)
    {
        if (testCase.Clock is not ("hours" or "days"))
        {
            return;
        }

        var exclusive = testCase.ReferencedEmails()
            .Select(manifest.FindAccount)
            .OfType<QaAccount>()
            .Where(account => account is { Disposition: QaDisposition.Seed, Role: QaRole.Patient } && !QaSeedOwnership.IsShared(manifest, account.Email))
            .Select(account => EmailNormalization.Normalize(account.Email))
            .ToList();
        var anchors = await _db.Users.AsNoTracking()
            .Where(user => exclusive.Contains(user.Email))
            .Join(_db.ConsentRecords, user => user.Id, consent => consent.UserId, (_, consent) => consent.AcceptedAt)
            .ToListAsync(ct).ConfigureAwait(false);
        if (anchors.Count == 0)
        {
            return;
        }

        var anchor = anchors.Min();
        var stale = testCase.Clock == "hours"
            ? now - anchor > TimeSpan.FromHours(HoursClockToleranceHours)
            : DateOnly.FromDateTime(anchor + QaClock.LimaOffset) != DateOnly.FromDateTime(now + QaClock.LimaOffset);
        if (stale)
        {
            problems.Add($"los datos con reloj se anclaron hace {(now - anchor).TotalHours:0} h; corre reset-qa {testCase.Cp}");
        }
    }

    private async Task<Guid?> UserIdAsync(string email, CancellationToken ct)
    {
        var normalized = EmailNormalization.Normalize(email);
        return await _db.Users.AsNoTracking().Where(user => user.Email == normalized).Select(user => (Guid?)user.Id)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
    }
}
