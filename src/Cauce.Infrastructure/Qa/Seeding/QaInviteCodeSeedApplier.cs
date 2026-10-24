using System.Security.Cryptography;
using Cauce.Domain.Identity;
using Cauce.Infrastructure.Qa.Manifest;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Infrastructure.Qa.Seeding;

/// <summary>
/// Códigos de invitación del nutricionista dueño: vigente, vencido o ya canjeado. Un código canjeado queda a nombre
/// del primer paciente del manifiesto, sin crear vínculo.
/// </summary>
public sealed class QaInviteCodeSeedApplier : IQaSeedApplier
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int CodeLength = 8;

    /// <inheritdoc />
    public int Order => 30;

    /// <inheritdoc />
    public bool CanApply(QaSeed seed) => seed is QaInviteCodeSeed;

    /// <inheritdoc />
    public async Task ApplyAsync(QaSeed seed, QaSeedContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        var invite = (QaInviteCodeSeed)seed;
        var anchor = context.Clock.Anchor;
        var code = await UniqueCodeAsync(context, ct).ConfigureAwait(false);

        switch (invite.State)
        {
            case QaInviteCodeState.Valid:
                // La vigencia es siempre de 72 horas: se emite hace (72 - horas restantes) horas.
                var hoursLeft = Math.Clamp(invite.ValidHoursLeft ?? (int)InvitationCode.Validity.TotalHours, 1, (int)InvitationCode.Validity.TotalHours);
                context.Db.InvitationCodes.Add(InvitationCode.Generate(
                    Guid.NewGuid(), code, context.Owner.Id, anchor.AddHours(hoursLeft - InvitationCode.Validity.TotalHours), InvitationCode.Validity));
                break;
            case QaInviteCodeState.Expired:
                var expired = InvitationCode.Generate(
                    Guid.NewGuid(), code, context.Owner.Id, anchor.AddDays(-(invite.AgeDays ?? 4)), InvitationCode.Validity);
                expired.Expire(anchor);
                context.Db.InvitationCodes.Add(expired);
                break;
            case QaInviteCodeState.Used:
                var patientEmail = context.Manifest.SeededAccounts.First(account => account.Role == QaRole.Patient).Email;
                var patient = await context.RequireUserAsync(patientEmail, ct).ConfigureAwait(false);
                var used = InvitationCode.Generate(Guid.NewGuid(), code, context.Owner.Id, anchor.AddHours(-2), InvitationCode.Validity);
                used.MarkAsUsed(patient.Id, anchor.AddHours(-1));
                context.Db.InvitationCodes.Add(used);
                break;
        }
    }

    private static async Task<string> UniqueCodeAsync(QaSeedContext context, CancellationToken ct)
    {
        while (true)
        {
            var code = new string(Enumerable.Range(0, CodeLength)
                .Select(_ => Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)])
                .ToArray());
            var taken = context.Db.InvitationCodes.Local.Any(existing => existing.Code == code)
                || await context.Db.InvitationCodes.AnyAsync(existing => existing.Code == code, ct).ConfigureAwait(false);
            if (!taken)
            {
                return code;
            }
        }
    }
}
