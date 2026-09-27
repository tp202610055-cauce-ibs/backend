using Cauce.Application.Common.Exceptions;
using Cauce.Application.Common.Identity;
using Cauce.Application.Common.Interfaces.Identity;
using MediatR;

namespace Cauce.Application.Identity.UseCases.Logout;

/// <summary>
/// Handler del cierre de sesión. Revoca el refresh token en Keycloak.
/// </summary>
public sealed class LogoutCommandHandler : IRequestHandler<LogoutCommand, Unit>
{
    private readonly IKeycloakTokenClient _tokenClient;
    private readonly IAuthenticationAttemptContext _attemptContext;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    /// <param name="tokenClient">Cliente de tokens de Keycloak.</param>
    /// <param name="attemptContext">Contexto del intento, donde se deja la causa de un rechazo.</param>
    public LogoutCommandHandler(IKeycloakTokenClient tokenClient, IAuthenticationAttemptContext attemptContext)
    {
        _tokenClient = tokenClient;
        _attemptContext = attemptContext;
    }

    /// <inheritdoc />
    public async Task<Unit> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        if (!string.Equals(request.ClientId, OidcClients.ForChannel(request.Channel), StringComparison.Ordinal))
        {
            _attemptContext.RecordFailure(AuthFailureCauses.UnsupportedClient);
            throw new UnsupportedOidcClientException();
        }

        if (string.IsNullOrEmpty(request.RefreshToken))
        {
            // Solo llega vacío desde el portal, cuando la cookie ya no existe: no queda sesión que revocar.
            return Unit.Value;
        }

        await _tokenClient.LogoutAsync(request.RefreshToken, request.ClientId, cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }
}
