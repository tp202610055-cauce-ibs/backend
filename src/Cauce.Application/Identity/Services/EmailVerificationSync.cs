using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Domain.Identity;
using Microsoft.Extensions.Logging;

namespace Cauce.Application.Identity.Services;

/// <summary>
/// Implementación de <see cref="IEmailVerificationSync"/>. Keycloak es la fuente de verdad: el enlace de confirmación
/// lo procesa el realm sin pasar por el backend, así que la copia local se desactualiza en cuanto el paciente verifica.
/// </summary>
/// <remarks>
/// La sincronización es <b>unidireccional</b>: si Keycloak reporta como no verificado un correo que localmente sí lo
/// está, el valor local se conserva y se registra una advertencia. Des-verificar exige un flujo explícito y auditado.
/// </remarks>
public sealed class EmailVerificationSync : IEmailVerificationSync
{
    private readonly IKeycloakAdminClient _adminClient;
    private readonly ILogger<EmailVerificationSync> _logger;

    /// <summary>
    /// Inicializa el servicio con sus dependencias.
    /// </summary>
    public EmailVerificationSync(IKeycloakAdminClient adminClient, ILogger<EmailVerificationSync> logger)
    {
        _adminClient = adminClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task TrySyncAsync(User user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        try
        {
            var verifiedInKeycloak = await _adminClient
                .GetUserEmailVerifiedAsync(user.KeycloakId, ct)
                .ConfigureAwait(false);

            if (verifiedInKeycloak == user.EmailVerified)
            {
                return;
            }

            if (!verifiedInKeycloak)
            {
                _logger.LogWarning(
                    "Keycloak reports an unverified email for user {UserId} that is verified locally; keeping the local value.",
                    user.Id);
                return;
            }

            user.VerifyEmail();
            _logger.LogInformation("Synced emailVerified from Keycloak for user {UserId}.", user.Id);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Un fallo de la Admin API no debe convertir una sesión válida en un error: se degrada al valor local.
            _logger.LogWarning(
                exception,
                "Could not sync emailVerified from Keycloak for user {UserId}; continuing with the local value.",
                user.Id);
        }
    }
}
