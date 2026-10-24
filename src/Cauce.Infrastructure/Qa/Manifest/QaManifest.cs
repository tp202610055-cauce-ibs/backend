namespace Cauce.Infrastructure.Qa.Manifest;

/// <summary>
/// Manifiesto de QA ya leído: cuentas y casos de prueba con sus datos de siembra. No contiene contraseñas.
/// </summary>
public sealed record QaManifest(
    string ManifestVersion,
    IReadOnlyList<QaAccount> Accounts,
    IReadOnlyList<QaCase> Cases)
{
    /// <summary>
    /// Cuentas que se siembran.
    /// </summary>
    public IEnumerable<QaAccount> SeededAccounts => Accounts.Where(account => account.Disposition == QaDisposition.Seed);

    /// <summary>
    /// Cuentas que no deben existir antes de ejecutar su caso.
    /// </summary>
    public IEnumerable<QaAccount> MustNotExistAccounts =>
        Accounts.Where(account => account.Disposition == QaDisposition.MustNotExist);

    /// <summary>
    /// Busca una cuenta por correo, sin distinguir mayúsculas.
    /// </summary>
    public QaAccount? FindAccount(string email) =>
        Accounts.FirstOrDefault(account => string.Equals(account.Email, email, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Busca un caso por su código (<c>CP001</c>), sin distinguir mayúsculas.
    /// </summary>
    public QaCase? FindCase(string cp) =>
        Cases.FirstOrDefault(testCase => string.Equals(testCase.Cp, cp, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Casos que mencionan la cuenta, en sus cuentas, en sus correos que no deben existir o en su siembra.
    /// </summary>
    public IReadOnlyList<QaCase> CasesUsing(string email) =>
        Cases.Where(testCase => testCase.ReferencedEmails().Contains(email, StringComparer.OrdinalIgnoreCase)).ToList();
}

/// <summary>
/// Rol de una cuenta sembrada.
/// </summary>
public enum QaRole
{
    Patient,
    Nutritionist
}

/// <summary>
/// Qué hace la siembra con una cuenta: crearla o asegurar que no exista.
/// </summary>
public enum QaDisposition
{
    Seed,
    MustNotExist
}

/// <summary>
/// Estado en que nace una cuenta sembrada.
/// </summary>
public enum QaAccountStatus
{
    Active,
    Disabled,
    PendingActivation
}

/// <summary>
/// Cuenta del manifiesto. <see cref="Role"/> y <see cref="Status"/> son nulos en las cuentas que no deben existir.
/// </summary>
public sealed record QaAccount(
    string Email,
    QaRole? Role,
    QaDisposition Disposition,
    QaAccountStatus? Status,
    string? DisplayName,
    IReadOnlyList<string> UsedBy);

/// <summary>
/// Caso de prueba con sus cuentas, los correos que no deben existir y sus datos de siembra.
/// </summary>
public sealed record QaCase(
    string Cp,
    IReadOnlyList<string> Accounts,
    IReadOnlyList<string> MustNotExist,
    bool Consumed,
    string? Clock,
    IReadOnlyList<QaSeed> Seeds)
{
    /// <summary>
    /// Todos los correos que el caso menciona.
    /// </summary>
    public IEnumerable<string> ReferencedEmails() =>
        Accounts.Concat(MustNotExist).Concat(Seeds.SelectMany(seed => seed.ReferencedEmails())).Distinct(StringComparer.OrdinalIgnoreCase);
}
