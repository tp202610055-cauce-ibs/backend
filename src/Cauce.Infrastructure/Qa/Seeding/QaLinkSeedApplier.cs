using Cauce.Domain.Patients;
using Cauce.Domain.Patients.Enums;
using Cauce.Infrastructure.Qa.Manifest;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Infrastructure.Qa.Seeding;

/// <summary>
/// Vínculo activo entre el paciente dueño y su nutricionista, desde el momento indicado o desde el ancla.
/// Se escribe con la entidad, sin el servicio de asignación, para no generar avisos ni filas de outbox.
/// </summary>
public sealed class QaLinkSeedApplier : IQaSeedApplier
{
    /// <inheritdoc />
    public int Order => 20;

    /// <inheritdoc />
    public bool CanApply(QaSeed seed) => seed is QaLinkSeed;

    /// <inheritdoc />
    public async Task ApplyAsync(QaSeed seed, QaSeedContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        var link = (QaLinkSeed)seed;
        var nutritionist = await context.RequireUserAsync(link.Nutritionist, ct).ConfigureAwait(false);
        var patientId = context.Owner.Id;

        var alreadyLinked = context.Db.NutritionistPatients.Local.Any(existing => IsSameActiveLink(existing, nutritionist.Id, patientId))
            || await context.Db.NutritionistPatients.AnyAsync(
                existing => existing.NutritionistId == nutritionist.Id && existing.PatientId == patientId && existing.Status == AssignmentStatus.Active,
                ct).ConfigureAwait(false);
        if (alreadyLinked)
        {
            return;
        }

        var since = link.Since is null ? context.Clock.Anchor : context.Clock.Resolve(link.Since);
        context.Db.NutritionistPatients.Add(NutritionistPatient.Establish(Guid.NewGuid(), nutritionist.Id, patientId, null, since));
    }

    private static bool IsSameActiveLink(NutritionistPatient existing, Guid nutritionistId, Guid patientId) =>
        existing.NutritionistId == nutritionistId && existing.PatientId == patientId && existing.Status == AssignmentStatus.Active;
}
