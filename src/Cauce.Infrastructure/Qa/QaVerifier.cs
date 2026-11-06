using System.Globalization;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Domain.ClinicalRegistry.Enums;
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
/// Verificación de solo lectura (<c>verify-qa</c>): cuentas, su estado y el consentimiento, cuentas que no deben existir,
/// vínculos y su antigüedad, perfil y alergias, comidas, síntomas, historial de respaldo, sustitutos, recomendaciones por
/// estado, archivado y pasos, glosario, códigos, agendas y la edad del ancla con reloj.
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

            if (account.Role == QaRole.Patient
                && !await _db.ConsentRecords.AsNoTracking().AnyAsync(consent => consent.UserId == user.Id && consent.IsCurrent, ct).ConfigureAwait(false))
            {
                problems.Add($"{account.Email} no tiene el consentimiento vigente aceptado");
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
                    await CheckLinkAsync(link, now, problems, ct).ConfigureAwait(false);
                    break;
                case QaGlossarySeed glossary:
                    await CheckGlossaryAsync(glossary, problems, ct).ConfigureAwait(false);
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

        await CheckClinicalRecordsAsync(testCase, problems, ct).ConfigureAwait(false);

        foreach (var patient in testCase.Seeds.OfType<QaRecommendationSeed>().Select(seed => seed.Patient).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            await CheckRecommendationsAsync(manifest, patient, now, problems, ct).ConfigureAwait(false);
        }

        foreach (var recommendation in testCase.Seeds.OfType<QaRecommendationSeed>())
        {
            await CheckSupportingHistoryAsync(recommendation, now, problems, ct).ConfigureAwait(false);
        }

        foreach (var patient in testCase.Seeds.OfType<QaRecommendationSeed>()
                     .Where(seed => seed.Substitutes.Count > 0)
                     .GroupBy(seed => seed.Patient, StringComparer.OrdinalIgnoreCase))
        {
            await CheckSubstitutesAsync(patient.Key, patient.Sum(seed => seed.Substitutes.Count), problems, ct).ConfigureAwait(false);
        }

        foreach (var nutritionist in testCase.Seeds.OfType<QaInviteCodeSeed>().Select(seed => seed.Nutritionist).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            await CheckInviteCodesAsync(manifest, nutritionist, now, problems, ct).ConfigureAwait(false);
        }
    }

    // Un vínculo con antigüedad debe tenerla todavía; uno con fecha fija descarta registros anteriores a esa fecha.
    private async Task CheckLinkAsync(QaLinkSeed link, DateTime now, List<string> problems, CancellationToken ct)
    {
        var patientId = await UserIdAsync(link.Patient, ct).ConfigureAwait(false);
        var nutritionistId = await UserIdAsync(link.Nutritionist, ct).ConfigureAwait(false);
        var assignedAt = await _db.NutritionistPatients.AsNoTracking()
            .Where(candidate => candidate.PatientId == patientId && candidate.NutritionistId == nutritionistId && candidate.Status == AssignmentStatus.Active)
            .Select(candidate => (DateTime?)candidate.AssignedAt)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (assignedAt is null)
        {
            problems.Add($"falta el vínculo de {link.Patient} con {link.Nutritionist}");
            return;
        }

        if (link.Since is null)
        {
            return;
        }

        if (assignedAt > new QaClock(now).Resolve(link.Since).AddHours(1))
        {
            problems.Add($"el vínculo de {link.Patient} es más reciente de lo que pide el caso");
        }

        if (link.Since.Date is { } since)
        {
            var start = QaClock.AtLimaTime(since, TimeOnly.MinValue);
            var earlier = await _db.Meals.AsNoTracking().AnyAsync(meal => meal.PatientId == patientId && meal.ConsumedAt < start, ct).ConfigureAwait(false)
                || await _db.Symptoms.AsNoTracking().AnyAsync(symptom => symptom.PatientId == patientId && symptom.OccurredAt < start, ct).ConfigureAwait(false);
            if (earlier)
            {
                problems.Add($"{link.Patient} tiene registros anteriores al {since.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}");
            }
        }
    }

    // Cuenta, por paciente, las comidas de cada alimento y los síntomas de cada tipo que el caso siembra.
    private async Task CheckClinicalRecordsAsync(QaCase testCase, List<string> problems, CancellationToken ct)
    {
        foreach (var group in testCase.Seeds.OfType<QaMealSeed>().GroupBy(meal => (meal.Patient, meal.Food)))
        {
            var patientId = await UserIdAsync(group.Key.Patient, ct).ConfigureAwait(false);
            var count = await _db.Meals.AsNoTracking()
                .Where(meal => meal.PatientId == patientId)
                .SelectMany(meal => meal.Items)
                .Join(_db.FoodItems, item => item.FoodId, food => food.Id, (_, food) => food.Name)
                .CountAsync(name => name == group.Key.Food, ct).ConfigureAwait(false);
            if (count < group.Count())
            {
                problems.Add($"{group.Key.Patient} tiene {count} comida(s) de «{group.Key.Food}» y debe tener {group.Count()}");
            }
        }

        foreach (var group in testCase.Seeds.OfType<QaSymptomSeed>().GroupBy(symptom => (symptom.Patient, symptom.Name)))
        {
            if (!QaManifestValidator.TryParseSymptom(group.Key.Name, out var type))
            {
                continue;
            }

            var patientId = await UserIdAsync(group.Key.Patient, ct).ConfigureAwait(false);
            var count = await _db.Symptoms.AsNoTracking().CountAsync(symptom => symptom.PatientId == patientId && symptom.SymptomType == type, ct).ConfigureAwait(false);
            if (count < group.Count())
            {
                problems.Add($"{group.Key.Patient} tiene {count} síntoma(s) «{group.Key.Name}» y debe tener {group.Count()}");
            }
        }
    }

    // El historial de respaldo se cuenta dentro de su ventana desde ahora, como lo calcula el detalle de la recomendación.
    private async Task CheckSupportingHistoryAsync(QaRecommendationSeed seed, DateTime now, List<string> problems, CancellationToken ct)
    {
        var expected = QaRecommendationSeedApplier.SupportingHistoryMoments(seed, new QaClock(now)).Count;
        if (expected == 0)
        {
            return;
        }

        var windowStart = now.AddDays(-(seed.SupportingData?.WindowDays ?? 7));
        var patientId = await UserIdAsync(seed.Patient, ct).ConfigureAwait(false);
        var count = await _db.Symptoms.AsNoTracking()
            .CountAsync(symptom => symptom.PatientId == patientId && symptom.SymptomType == SymptomType.AbdominalPain && symptom.OccurredAt >= windowStart, ct)
            .ConfigureAwait(false);
        if (count < expected)
        {
            problems.Add($"{seed.Patient} tiene {count} registro(s) de dolor abdominal en la ventana y debe tener {expected}");
        }
    }

    private async Task CheckSubstitutesAsync(string patient, int expected, List<string> problems, CancellationToken ct)
    {
        var patientId = await UserIdAsync(patient, ct).ConfigureAwait(false);
        var count = await _db.Recommendations.AsNoTracking()
            .Where(recommendation => recommendation.PatientId == patientId)
            .SelectMany(recommendation => recommendation.Items)
            .CountAsync(item => item.ActionType == ActionType.Substitute && item.SubstituteFoodId != null && item.Reasoning != null, ct)
            .ConfigureAwait(false);
        if (count < expected)
        {
            problems.Add($"{patient} tiene {count} sustituto(s) con justificación y debe tener {expected}");
        }
    }

    private async Task CheckGlossaryAsync(QaGlossarySeed seed, List<string> problems, CancellationToken ct)
    {
        var terms = await _db.GlossaryTerms.AsNoTracking().Select(term => new { term.Term, term.Category }).ToListAsync(ct).ConfigureAwait(false);
        var match = terms.FirstOrDefault(term => string.Equals(term.Term, seed.Term, StringComparison.OrdinalIgnoreCase));
        if (!seed.Present)
        {
            if (match is not null)
            {
                problems.Add($"el glosario tiene «{seed.Term}» y no debe tenerlo");
            }

            return;
        }

        if (match is null)
        {
            problems.Add($"el glosario no tiene «{seed.Term}»");
        }
        else if (seed.Category is not null && QaManifestValidator.TryParseGlossaryCategory(seed.Category, out var category) && match.Category != category)
        {
            problems.Add($"«{seed.Term}» es {match.Category} en el glosario y debe ser {category}");
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

        if (profile is null || (seed.OnboardingCompleted && !profile.OnboardingCompleted))
        {
            problems.Add($"{seed.Patient} no tiene el perfil completo");
            return;
        }

        if (!seed.OnboardingCompleted && profile.OnboardingCompleted)
        {
            problems.Add($"{seed.Patient} ya tiene el onboarding completo y no debe tenerlo");
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
        var seeds = QaSeedOwnership.OwnedBy(manifest, patient)
            .Select(pair => pair.Seed)
            .OfType<QaRecommendationSeed>()
            .ToList();
        var expected = seeds
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

        // Ninguna sembrada nace archivada; una manual con la vigencia vencida espera el archivado (CP075).
        var archived = actual.Count(recommendation => !recommendation.IsActive);
        if (archived > 0)
        {
            problems.Add($"{patient} tiene {archived} recomendación(es) archivada(s) y debe tener 0");
        }

        var pendingArchive = actual.Count(recommendation => recommendation.IsActive && recommendation.ValidUntil < now);
        var expectedPendingArchive = seeds.Count(seed => seed is { IsManual: true, ValidDaysLeft: < 0 });
        if (pendingArchive != expectedPendingArchive)
        {
            problems.Add($"{patient} tiene {pendingArchive} recomendación(es) vencida(s) pendiente(s) de archivo y debe tener {expectedPendingArchive}");
        }

        // Cada recomendación con pasos en el caso debe existir con esos pasos, en el mismo orden.
        var stepLists = actual.Where(recommendation => recommendation.Steps is { Count: > 0 })
            .Select(recommendation => string.Join('\n', recommendation.Steps!))
            .ToList();
        foreach (var seed in seeds.Where(seed => seed.SeededSteps.Count > 0))
        {
            if (!stepLists.Remove(string.Join('\n', seed.SeededSteps)))
            {
                problems.Add($"{patient} no tiene la recomendación «{seed.Title}» con sus {seed.SeededSteps.Count} paso(s)");
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
