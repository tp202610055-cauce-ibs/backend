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

    /// <summary>
    /// Refresh token considerado válido por el doble. Cualquier otro valor provoca
    /// <see cref="InvalidRefreshTokenException"/>.
    /// </summary>
    public string ValidRefreshToken { get; set; } = "fake-refresh-token";

    /// <summary>
    /// Sujeto de Keycloak que el doble incrusta en el token renovado. Las pruebas lo fijan al
    /// <c>keycloak_id</c> del usuario sembrado para que el handler resuelva su cuenta local.
    /// </summary>
    public string Subject { get; set; } = "fake-subject";

    /// <summary>
    /// Último scope solicitado en un inicio de sesión, para verificar que el móvil pide
    /// <c>offline_access</c>. El doble reproduce aquí la misma decisión que toma el cliente real.
    /// </summary>
    public string? LastLoginScope { get; private set; }

    /// <inheritdoc />
    public Task<KeycloakTokenResult> LoginAsync(string email, string password, string clientId, CancellationToken ct = default)
    {
        if (!string.Equals(password, ValidPassword, StringComparison.Ordinal))
        {
            throw new InvalidCredentialsException();
        }

        LastLoginScope = clientId == "cauce-mobile" ? "openid offline_access" : "openid";

        return Task.FromResult(new KeycloakTokenResult(
            AccessToken: BuildAccessToken(),
            RefreshToken: ValidRefreshToken,
            ExpiresIn: 900,
            RefreshExpiresIn: 2592000,
            TokenType: "Bearer",
            Subject: Subject));
    }

    /// <inheritdoc />
    public Task<KeycloakTokenResult> RefreshAsync(string refreshToken, string clientId, CancellationToken ct = default)
    {
        if (!string.Equals(refreshToken, ValidRefreshToken, StringComparison.Ordinal))
        {
            throw new InvalidRefreshTokenException();
        }

        return Task.FromResult(new KeycloakTokenResult(
            AccessToken: BuildAccessToken(),
            RefreshToken: ValidRefreshToken + "-rotated",
            ExpiresIn: 900,
            RefreshExpiresIn: 2592000,
            TokenType: "Bearer",
            Subject: Subject));
    }

    private string BuildAccessToken() => $"fake-access-token-{Subject}";

    /// <inheritdoc />
    public Task LogoutAsync(string refreshToken, string clientId, CancellationToken ct = default)
    {
        LogoutCount++;
        return Task.CompletedTask;
    }
}
