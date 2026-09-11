using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Domain.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using IdentityOptions = Cauce.Infrastructure.Identity.DevAdminOptions;

namespace Cauce.Infrastructure.Persistence.Seeders;

/// <summary>
/// Seeder de desarrollo que provisiona un nutricionista de prueba con credenciales
/// fijas. Es idempotente y solo debe ejecutarse en el entorno de desarrollo.
/// </summary>
public sealed class DevAdminSeeder
{
    private readonly IUserRepository _userRepository;
    private readonly IKeycloakAdminClient _keycloakAdminClient;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOptions<IdentityOptions> _options;
    private readonly ILogger<DevAdminSeeder> _logger;

    /// <summary>
    /// Inicializa el seeder con sus dependencias.
    /// </summary>
    public DevAdminSeeder(
        IUserRepository userRepository,
        IKeycloakAdminClient keycloakAdminClient,
        IUnitOfWork unitOfWork,
        IOptions<IdentityOptions> options,
        ILogger<DevAdminSeeder> logger)
    {
        _userRepository = userRepository;
        _keycloakAdminClient = keycloakAdminClient;
        _unitOfWork = unitOfWork;
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// Provisiona el nutricionista de prueba si está habilitado y aún no existe.
    /// </summary>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Tarea que representa la operación asíncrona.</returns>
    public async Task SeedAsync(CancellationToken ct = default)
    {
        var options = _options.Value;
        if (!options.Enabled || string.IsNullOrWhiteSpace(options.Email))
        {
            return;
        }

        if (await _userRepository.ExistsByEmailAsync(options.Email, ct).ConfigureAwait(false))
        {
            return;
        }

        var nutritionistRoleId = await _userRepository
            .GetRoleIdAsync(UserRoles.Nutritionist, ct)
            .ConfigureAwait(false);

        var keycloakId = await _keycloakAdminClient
            .CreateUserAsync(options.Email, options.FullName, UserRoles.Nutritionist, requireEmailVerification: false, ct)
            .ConfigureAwait(false);

        await _keycloakAdminClient
            .SetTemporaryPasswordAsync(keycloakId, options.TemporaryPassword, ct)
            .ConfigureAwait(false);

        var user = User.CreateNutritionist(Guid.NewGuid(), keycloakId, options.Email, options.FullName, nutritionistRoleId);

        // El nutricionista de desarrollo no pasa por el enlace de Keycloak: usa una contraseña temporal
        // fija. Se activa aquí porque DemoPatientSeeder solo asigna el paciente demo a un nutricionista
        // activo (acta A51).
        user.Activate();
        await _userRepository.AddAsync(user, ct).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);

        _logger.LogInformation("Seeded development nutritionist {UserId}.", user.Id);
    }
}
