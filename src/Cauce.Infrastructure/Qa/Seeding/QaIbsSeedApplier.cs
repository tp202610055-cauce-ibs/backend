using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry.Enums;
using Cauce.Infrastructure.Qa.Manifest;

namespace Cauce.Infrastructure.Qa.Seeding;

/// <summary>
/// Cuestionarios IBS-SSS respondidos y agendas abiertas: la que vence hoy y la que venció sin respuesta.
/// </summary>
public sealed class QaIbsSeedApplier : IQaSeedApplier
{
    /// <summary>
    /// Días entre un cuestionario periódico y el siguiente.
    /// </summary>
    public const int CycleDays = 14;

    /// <inheritdoc />
    public int Order => 30;

    /// <inheritdoc />
    public bool CanApply(QaSeed seed) => seed is QaIbsSeed or QaIbsDueSeed or QaIbsInvitationSeed;

    /// <inheritdoc />
    public Task ApplyAsync(QaSeed seed, QaSeedContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        var patientId = context.Owner.Id;
        switch (seed)
        {
            case QaIbsSeed ibs:
                var cycle = ibs.Baseline
                    ? 0
                    : 1 + context.Db.IbsSssAssessments.Local.Count(assessment =>
                        assessment.PatientId == patientId && assessment.AssessmentType == AssessmentType.Periodic);
                var dimensions = SplitScore(ibs.Score);
                context.Db.IbsSssAssessments.Add(IbsSssAssessment.Submit(
                    Guid.NewGuid(),
                    patientId,
                    ibs.Baseline ? AssessmentType.Baseline : AssessmentType.Periodic,
                    cycle,
                    dimensions[0],
                    dimensions[1],
                    dimensions[2],
                    dimensions[3],
                    dimensions[4],
                    context.Clock.Resolve(ibs.Ago)));
                break;
            case QaIbsDueSeed:
                AddSchedule(context, context.Clock.Anchor);
                break;
            case QaIbsInvitationSeed invitation when !invitation.Answered:
                AddSchedule(context, context.Clock.Resolve(invitation.SentAgo));
                break;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Reparte un puntaje total en las cinco dimensiones de 0 a 100; el resto de la división va a la primera.
    /// </summary>
    public static int[] SplitScore(int score)
    {
        var share = score / 5;
        var dimensions = Enumerable.Repeat(share, 5).ToArray();
        dimensions[0] += score - (share * 5);
        return dimensions;
    }

    private static void AddSchedule(QaSeedContext context, DateTime dueDate)
    {
        context.Db.IbsSssSchedules.Add(IbsSssAssessmentSchedule.Create(
            Guid.NewGuid(), context.Owner.Id, dueDate, dueDate.AddDays(-CycleDays)));
    }
}
