using Cauce.Infrastructure.Qa.Manifest;

namespace Cauce.Infrastructure.Qa.Seeding;

/// <summary>
/// Qué cuenta es dueña de cada elemento de siembra y qué cuentas comparten varios casos.
/// </summary>
public static class QaSeedOwnership
{
    /// <summary>
    /// Correo de la cuenta dueña del elemento, o <see langword="null"/> si no siembra datos (notas y estados de cuenta).
    /// </summary>
    public static string? OwnerOf(QaSeed seed) => seed switch
    {
        QaNoteSeed or QaAccountSeed => null,
        QaInviteCodeSeed invite => invite.Nutritionist,
        QaLinkSeed link => link.Patient,
        _ => seed.ReferencedEmails().FirstOrDefault()
    };

    /// <summary>
    /// Elementos que pertenecen a la cuenta, con el caso de origen, en el orden del manifiesto.
    /// </summary>
    public static IReadOnlyList<(string Cp, QaSeed Seed)> OwnedBy(QaManifest manifest, string email, IEnumerable<string>? onlyCases = null)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var cases = onlyCases?.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return manifest.Cases
            .Where(testCase => cases is null || cases.Contains(testCase.Cp))
            .SelectMany(testCase => testCase.Seeds.Select(seed => (testCase.Cp, Seed: seed)))
            .Where(pair => string.Equals(OwnerOf(pair.Seed), email, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// Turno de la cuenta en una siembra: nutricionistas, luego pacientes, y al final los nutricionistas con
    /// códigos ya canjeados, porque esos códigos apuntan a un paciente.
    /// </summary>
    public static int ProcessingRank(QaManifest manifest, QaAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);
        if (account.Role == QaRole.Patient)
        {
            return 1;
        }

        var handsOutUsedCodes = OwnedBy(manifest, account.Email)
            .Any(pair => pair.Seed is QaInviteCodeSeed { State: QaInviteCodeState.Used });
        return handsOutUsedCodes ? 2 : 0;
    }

    /// <summary>
    /// Indica si la cuenta aparece en más de un caso. Las compartidas no se borran al rearmar un caso.
    /// </summary>
    public static bool IsShared(QaManifest manifest, string email)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        return manifest.CasesUsing(email).Count > 1;
    }
}
