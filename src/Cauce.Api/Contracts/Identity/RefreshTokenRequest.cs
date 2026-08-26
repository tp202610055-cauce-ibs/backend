namespace Cauce.Api.Contracts.Identity;

/// <summary>
/// Cuerpo de la petición de renovación de sesión. El realm tiene rotación de refresh tokens
/// activada, así que la respuesta trae un token nuevo y el enviado aquí queda revocado.
/// </summary>
/// <param name="RefreshToken">Refresh token vigente, obtenido del inicio de sesión o de una renovación previa.</param>
/// <param name="ClientId">Identificador del cliente OIDC (<c>cauce-mobile</c> o <c>cauce-web-portal</c>).</param>
public sealed record RefreshTokenRequest(string RefreshToken, string ClientId);
