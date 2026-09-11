using System.Text.Json;
using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Domain.Auditing.Enums;
using Cauce.Domain.Identity;
using Cauce.Domain.Identity.Enums;
using Microsoft.Extensions.Logging;

namespace Cauce.Application.Identity.Services;

/// <summary>
/// Implementación de <see cref="INutritionistActivationService"/> (acta A51).
/// </summary>
public sealed class NutritionistActivationService : INutritionistActivationService
{
    private readonly IUserRepository _userRepository;
    private readonly IAuditLogger _auditLogger;
    private readonly ILogger<NutritionistActivationService> _logger;

    /// <summary>
    /// Inicializa el servicio con sus dependencias.
    /// </summary>
    /// <param name="userRepository">Repositorio de cuentas, para resolver el rol nutricionista.</param>
    /// <param name="auditLogger">Registrador de auditoría.</param>
    /// <param name="logger">Logger de la categoría del servicio.</param>
    public NutritionistActivationService(
        IUserRepository userRepository,
        IAuditLogger auditLogger,
        ILogger<NutritionistActivationService> logger)
    {
        _userRepository = userRepository;
        _auditLogger = auditLogger;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<bool> ActivateIfPendingAsync(
        User user,
        NutritionistActivationTrigger trigger,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        // El estado se mira primero porque no cuesta nada: casi todas las llamadas llegan con la cuenta ya
        // activa y no deben pagar una consulta.
        if (user.Status != UserStatus.PendingActivation)
        {
            return false;
        }

        var nutritionistRoleId = await _userRepository
            .GetRoleIdAsync(UserRoles.Nutritionist, ct)
            .ConfigureAwait(false);
        if (user.RoleId != nutritionistRoleId)
        {
            // Un paciente pendiente se activa al verificar su correo (VerifyEmail), no por autenticarse.
            return false;
        }

        if (!user.EmailVerified)
        {
            // No debería ocurrir: el nutricionista se provisiona con el correo verificado. Activate() lo
            // rechazaría, y un inicio de sesión válido no debe convertirse en un error por esto.
            _logger.LogWarning(
                "Pending nutritionist {UserId} has an unverified email; activation skipped.",
                user.Id);
            return false;
        }

        user.Activate();

        // users no tiene trigger: sin esta fila la transición de estado no dejaría rastro (DEC-B5-01 capa 3).
        await _auditLogger.LogAsync(
            AuditActionType.AccountActivation,
            nameof(User),
            user.Id,
            oldValuesHash: null,
            newValuesHash: null,
            additionalContext: JsonSerializer.Serialize(new { trigger = ToWireValue(trigger) }),
            actorUserId: user.Id,
            cancellationToken: ct).ConfigureAwait(false);

        _logger.LogInformation("Nutritionist {UserId} activated on {Trigger}.", user.Id, trigger);
        return true;
    }

    private static string ToWireValue(NutritionistActivationTrigger trigger) => trigger switch
    {
        NutritionistActivationTrigger.Login => "login",
        NutritionistActivationTrigger.AuthenticatedRequest => "authenticated_request",
        _ => throw new ArgumentOutOfRangeException(nameof(trigger), trigger, null)
    };
}
