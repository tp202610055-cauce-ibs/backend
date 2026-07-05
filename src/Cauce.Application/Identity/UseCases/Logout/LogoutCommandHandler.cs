using Cauce.Application.Common.Interfaces.Identity;
using MediatR;

namespace Cauce.Application.Identity.UseCases.Logout;

/// <summary>
/// Handler del cierre de sesión. Revoca el refresh token en Keycloak.
/// </summary>
public sealed class LogoutCommandHandler : IRequestHandler<LogoutCommand, Unit>
{
    private readonly IKeycloakTokenClient _tokenClient;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    /// <param name="tokenClient">Cliente de tokens de Keycloak.</param>
    public LogoutCommandHandler(IKeycloakTokenClient tokenClient)
    {
        _tokenClient = tokenClient;
    }

    /// <inheritdoc />
    public async Task<Unit> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        await _tokenClient.LogoutAsync(request.RefreshToken, request.ClientId, cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }
}
