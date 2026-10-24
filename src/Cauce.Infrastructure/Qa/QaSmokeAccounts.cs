using Cauce.Infrastructure.Qa.Manifest;

namespace Cauce.Infrastructure.Qa;

/// <summary>
/// Cuentas fijas para la prueba de humo del portal, separadas de las de los casos de prueba. Usan <c>QA_SMOKE_PASSWORD</c>.
/// </summary>
public static class QaSmokeAccounts
{
    /// <summary>
    /// Nutricionista activo, paciente activo, nutricionista deshabilitado y nutricionista pendiente de activación.
    /// </summary>
    public static IReadOnlyList<QaAccount> All { get; } =
    [
        Smoke("smoke.nutricionista@ejemplo.com", QaRole.Nutritionist, QaAccountStatus.Active),
        Smoke("smoke.paciente@ejemplo.com", QaRole.Patient, QaAccountStatus.Active),
        Smoke("smoke.nutricionista.inactivo@ejemplo.com", QaRole.Nutritionist, QaAccountStatus.Disabled),
        Smoke("smoke.nutricionista.pendiente@ejemplo.com", QaRole.Nutritionist, QaAccountStatus.PendingActivation)
    ];

    /// <summary>
    /// Indica si el correo es de una cuenta de humo.
    /// </summary>
    public static bool IsSmoke(string email) =>
        All.Any(account => string.Equals(account.Email, email, StringComparison.OrdinalIgnoreCase));

    private static QaAccount Smoke(string email, QaRole role, QaAccountStatus status) =>
        new(email, role, QaDisposition.Seed, status, DisplayName: null, UsedBy: []);
}
