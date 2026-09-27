using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Domain.Identity;
using Cauce.Infrastructure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cauce.Infrastructure.Persistence.Seeders;

/// <summary>
/// Seeder de desarrollo que provisiona dos nutricionistas para los casos de prueba de acceso al portal
/// (acta A68): uno deshabilitado en Keycloak y suspendido en el backend, con contraseña, y otro pendiente
/// de activación, sin contraseña. Sigue el patrón de <see cref="DemoPatientSeeder"/>: solo corre en
/// Development, lo gatea <c>QaNutritionists:Enabled</c>, es idempotente por correo y reutiliza el usuario
/// de Keycloak si un intento previo lo dejó creado.
/// </summary>
/// <remarks>
/// El backend no tiene una transición a <c>Inactive</c> para nutricionistas: la única salida de
/// <c>Active</c> es <see cref="User.Suspend"/>, y el flujo de suspensión sigue diferido (actas A55 y A56).
/// Por eso la cuenta deshabilitada queda <c>Suspended</c>, que para el portal es tan "no activa" como
/// <c>Inactive</c>.
/// </remarks>
public sealed class QaNutritionistsSeeder
{
    private readonly IUserRepository _userRepository;
    private readonly IKeycloakAdminClient _keycloakAdminClient;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOptions<QaNutritionistsOptions> _options;
    private readonly ILogger<QaNutritionistsSeeder> _logger;

    /// <summary>
    /// Inicializa el seeder con sus dependencias.
    /// </summary>
    public QaNutritionistsSeeder(
        IUserRepository userRepository,
        IKeycloakAdminClient keycloakAdminClient,
        IUnitOfWork unitOfWork,
        IOptions<QaNutritionistsOptions> options,
        ILogger<QaNutritionistsSeeder> logger)
    {
        _userRepository = userRepository;
        _keycloakAdminClient = keycloakAdminClient;
        _unitOfWork = unitOfWork;
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// Provisiona las cuentas de prueba que estén configuradas y todavía no existan.
    /// </summary>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Tarea que representa la operación asíncrona.</returns>
    public async Task SeedAsync(CancellationToken ct = default)
    {
        var options = _options.Value;
        if (!options.Enabled)
        {
            return;
        }

        var nutritionistRoleId = await _userRepository
            .GetRoleIdAsync(UserRoles.Nutritionist, ct)
            .ConfigureAwait(false);

        await SeedDisabledAsync(options, nutritionistRoleId, ct).ConfigureAwait(false);
        await SeedPendingAsync(options, nutritionistRoleId, ct).ConfigureAwait(false);
    }

    private async Task SeedDisabledAsync(QaNutritionistsOptions options, int nutritionistRoleId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(options.InactiveEmail)
            || await _userRepository.ExistsByEmailAsync(options.InactiveEmail, ct).ConfigureAwait(false))
        {
            return;
        }

        var keycloakId = await ResolveKeycloakUserAsync(options.InactiveEmail, options.InactiveFullName, ct)
            .ConfigureAwait(false);

        // Con contraseña: el caso de prueba entra con las credenciales correctas y aun así debe fallar.
        await _keycloakAdminClient.ResetPasswordAsync(keycloakId, options.Password, ct).ConfigureAwait(false);
        await _keycloakAdminClient.DisableUserAsync(keycloakId, ct).ConfigureAwait(false);

        var user = User.CreateNutritionist(
            Guid.NewGuid(), keycloakId, options.InactiveEmail, options.InactiveFullName, nutritionistRoleId);
        user.Activate();
        user.Suspend();
        await _userRepository.AddAsync(user, ct).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);

        _logger.LogInformation("Seeded disabled QA nutritionist {UserId}.", user.Id);
    }

    private async Task SeedPendingAsync(QaNutritionistsOptions options, int nutritionistRoleId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(options.PendingEmail)
            || await _userRepository.ExistsByEmailAsync(options.PendingEmail, ct).ConfigureAwait(false))
        {
            return;
        }

        // Sin contraseña y sin correo de activación: queda exactamente como una provisión cuyo enlace nadie
        // usó todavía (acta A52), pero sin llenar Mailpit en cada instalación.
        var keycloakId = await ResolveKeycloakUserAsync(options.PendingEmail, options.PendingFullName, ct)
            .ConfigureAwait(false);

        var user = User.CreateNutritionist(
            Guid.NewGuid(), keycloakId, options.PendingEmail, options.PendingFullName, nutritionistRoleId);
        await _userRepository.AddAsync(user, ct).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);

        _logger.LogInformation("Seeded pending QA nutritionist {UserId}.", user.Id);
    }

    private async Task<string> ResolveKeycloakUserAsync(string email, string fullName, CancellationToken ct)
    {
        var existing = await _keycloakAdminClient.FindByEmailAsync(email, ct).ConfigureAwait(false);
        return existing?.Id
            ?? await _keycloakAdminClient
                .CreateUserAsync(email, fullName, UserRoles.Nutritionist, requireEmailVerification: false, ct)
                .ConfigureAwait(false);
    }
}
