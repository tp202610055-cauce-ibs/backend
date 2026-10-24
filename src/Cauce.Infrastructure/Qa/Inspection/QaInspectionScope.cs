using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Domain.Identity;
using Cauce.Infrastructure.Persistence;
using Cauce.Infrastructure.Qa.Manifest;
using Cauce.Infrastructure.Qa.Seeding;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Infrastructure.Qa.Inspection;

/// <summary>
/// Cuenta del manifiesto resuelta contra la base. <see cref="UserId"/> es nulo si la cuenta no existe en el backend.
/// </summary>
public sealed record QaResolvedAccount(string Email, QaRole? Role, QaDisposition Disposition, bool Shared, Guid? UserId);

/// <summary>
/// Lo que inspeccionan <c>audit-log</c> e <c>inspect-qa</c>: un caso del manifiesto o una sola cuenta, con sus cuentas
/// resueltas. Solo lee.
/// </summary>
public sealed record QaInspectionScope(string Target, QaCase? Case, IReadOnlyList<QaResolvedAccount> Accounts)
{
    /// <summary>
    /// Pacientes del alcance que existen en el backend.
    /// </summary>
    public IReadOnlyList<Guid> PatientIds =>
        Accounts.Where(account => account is { Role: QaRole.Patient, UserId: not null }).Select(account => account.UserId!.Value).ToList();

    /// <summary>
    /// Indica si el alcance incluye cuentas que no deben existir, como en las pruebas con un correo sin cuenta.
    /// </summary>
    public bool HasMustNotExist => Accounts.Any(account => account.Disposition == QaDisposition.MustNotExist);

    /// <summary>
    /// Correo de la cuenta del manifiesto con ese identificador, o nulo si no es del alcance.
    /// </summary>
    public string? EmailOf(Guid? userId) =>
        userId is null ? null : Accounts.FirstOrDefault(account => account.UserId == userId)?.Email;

    /// <summary>
    /// Resuelve un caso (<c>CPxxx</c>) o un correo del manifiesto. Una cuenta de paciente anonimizada por la baja se
    /// encuentra por su identidad en Keycloak, que conserva el correo.
    /// </summary>
    /// <exception cref="QaManifestException">Si el objetivo no es un caso ni una cuenta del manifiesto.</exception>
    public static async Task<QaInspectionScope> ResolveAsync(
        QaManifest manifest, string target, CauceDbContext db, IKeycloakAdminClient keycloak, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(target);
        var testCase = manifest.FindCase(target);
        var emails = testCase?.ReferencedEmails().ToList()
            ?? (manifest.FindAccount(target) is not null ? [target] : throw new QaManifestException([$"Objetivo desconocido: {target}."]));

        var accounts = new List<QaResolvedAccount>();
        foreach (var account in emails.Select(manifest.FindAccount).OfType<QaAccount>())
        {
            var userId = await FindUserIdAsync(account.Email, db, keycloak, ct).ConfigureAwait(false);
            accounts.Add(new QaResolvedAccount(account.Email, account.Role, account.Disposition, QaSeedOwnership.IsShared(manifest, account.Email), userId));
        }

        return new QaInspectionScope(testCase?.Cp ?? target, testCase, accounts);
    }

    private static async Task<Guid?> FindUserIdAsync(string email, CauceDbContext db, IKeycloakAdminClient keycloak, CancellationToken ct)
    {
        var normalized = EmailNormalization.Normalize(email);
        var userId = await db.Users.AsNoTracking().Where(user => user.Email == normalized).Select(user => (Guid?)user.Id)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (userId is not null)
        {
            return userId;
        }

        var identity = await keycloak.FindByEmailAsync(normalized, ct).ConfigureAwait(false);
        return identity is null
            ? null
            : await db.Users.AsNoTracking().Where(user => user.KeycloakId == identity.Id).Select(user => (Guid?)user.Id)
                .FirstOrDefaultAsync(ct).ConfigureAwait(false);
    }
}
