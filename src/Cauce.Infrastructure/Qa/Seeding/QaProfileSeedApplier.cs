using Cauce.Domain.Patients;
using Cauce.Domain.Patients.Enums;
using Cauce.Infrastructure.Qa.Manifest;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Infrastructure.Qa.Seeding;

/// <summary>
/// Perfil clínico con el onboarding terminado y sus alergias declaradas. Un perfil incompleto no se siembra:
/// el paciente queda sin ningún avance de onboarding guardado. Si el paciente ya tiene perfil, no se toca.
/// </summary>
public sealed class QaProfileSeedApplier : IQaSeedApplier
{
    /// <summary>
    /// Severidad de cada alergia sembrada; el manifiesto no la trae.
    /// </summary>
    public const AllergySeverity DefaultSeverity = AllergySeverity.Moderate;

    /// <inheritdoc />
    public int Order => 10;

    /// <inheritdoc />
    public bool CanApply(QaSeed seed) => seed is QaProfileSeed;

    /// <inheritdoc />
    public async Task ApplyAsync(QaSeed seed, QaSeedContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        var profile = (QaProfileSeed)seed;
        var patientId = context.Owner.Id;
        var exists = context.Db.PatientProfiles.Local.Any(existing => existing.UserId == patientId)
            || await context.Db.PatientProfiles.AnyAsync(existing => existing.UserId == patientId, ct).ConfigureAwait(false);
        if (!profile.Complete || exists)
        {
            return;
        }

        var subtype = profile.Subtype is not null && QaManifestValidator.TryParseSubtype(profile.Subtype, out var parsed)
            ? parsed
            : QaDefaults.Subtype;
        var now = context.Clock.Anchor;

        var patientProfile = PatientProfile.Create(
            Guid.NewGuid(),
            context.Owner.Id,
            QaDefaults.DateOfBirth,
            QaDefaults.Sex,
            profile.WeightKg ?? QaDefaults.WeightKg,
            profile.HeightCm ?? QaDefaults.HeightCm,
            subtype,
            diagnosisDate: null,
            medications: null,
            now);
        patientProfile.CompleteOnboarding(now);
        context.Db.PatientProfiles.Add(patientProfile);

        foreach (var allergy in profile.Allergies)
        {
            context.Db.PatientAllergies.Add(PatientAllergy.Declare(
                Guid.NewGuid(), context.Owner.Id, context.Catalog.Allergies[allergy], DefaultSeverity, notes: null, now));
        }
    }
}
