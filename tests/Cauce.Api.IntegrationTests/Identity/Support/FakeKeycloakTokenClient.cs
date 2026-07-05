using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Domain.Identity.Exceptions;

namespace Cauce.Api.IntegrationTests.Identity.Support;

/// <summary>
/// Doble en memoria de <see cref="IKeycloakTokenClient"/> para pruebas de integración. Evita
/// contactar a Keycloak: devuelve tokens cuando la contraseña coincide con <see cref="ValidPassword"/>
/// y lanza <see cref="InvalidCredentialsException"/> en caso contrario (para ejercer FAILED_LOGIN).
/// </summary>
public sealed class FakeKeycloakTokenClient : IKeycloakTokenClient
{
    /// <summary>
    /// Contraseña considerada válida por el doble.
    /// </summary>
    public string ValidPassword { get; set; } = "Correct123!";

    /// <summary>
    /// Cantidad de cierres de sesión (revocaciones) solicitados.
    /// </summary>
    public int LogoutCount { get; private set; }

    /// <inheritdoc />
    public Task<KeycloakTokenResult> LoginAsync(string email, string password, string clientId, CancellationToken ct = default)
    {
        if (!string.Equals(password, ValidPassword, StringComparison.Ordinal))
        {
            throw new InvalidCredentialsException();
        }

        return Task.FromResult(new KeycloakTokenResult(
            AccessToken: "fake-access-token",
            RefreshToken: "fake-refresh-token",
            ExpiresIn: 900,
            RefreshExpiresIn: 2592000,
            TokenType: "Bearer"));
    }

    /// <inheritdoc />
    public Task LogoutAsync(string refreshToken, string clientId, CancellationToken ct = default)
    {
        LogoutCount++;
        return Task.CompletedTask;
    }
}
