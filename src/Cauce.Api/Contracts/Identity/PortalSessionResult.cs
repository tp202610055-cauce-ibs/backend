using Cauce.Application.Identity.UseCases.Login;

namespace Cauce.Api.Contracts.Identity;

/// <summary>
/// Respuesta del inicio de sesión y de la renovación del portal web. A diferencia de la del móvil, no
/// trae el refresh token: viaja en la cookie <c>HttpOnly</c> <c>cauce_portal_rt</c>, fuera del alcance del
/// JavaScript del portal (acta A68). El portal guarda el access token solo en memoria.
/// </summary>
/// <param name="AccessToken">Token de acceso JWT, para el header <c>Authorization: Bearer</c>.</param>
/// <param name="ExpiresIn">Vigencia del token de acceso, en segundos.</param>
/// <param name="TokenType">Tipo de token, <c>Bearer</c>.</param>
/// <param name="User">Identidad del nutricionista autenticado.</param>
public sealed record PortalSessionResult(
    string AccessToken,
    int ExpiresIn,
    string TokenType,
    AuthenticatedUser User);
