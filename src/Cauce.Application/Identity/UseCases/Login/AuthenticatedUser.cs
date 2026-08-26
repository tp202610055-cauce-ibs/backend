namespace Cauce.Application.Identity.UseCases.Login;

/// <summary>
/// Identidad del usuario que acompaña a los tokens en el inicio de sesión y en la renovación. Evita
/// que el cliente necesite una segunda llamada, o decodificar el JWT, para conocer quién inició
/// sesión.
/// </summary>
/// <param name="UserId">Identificador local de la cuenta.</param>
/// <param name="KeycloakId">Identificador del usuario en Keycloak (claim <c>sub</c> del JWT).</param>
/// <param name="Email">Correo electrónico de la cuenta.</param>
/// <param name="Role">Nombre canónico del rol (<c>patient</c> o <c>nutritionist</c>).</param>
/// <param name="FullName">Nombre completo del usuario.</param>
/// <param name="EmailVerified">Indica si el correo fue verificado.</param>
/// <param name="IsInActivePilot">
/// Indica si la cuenta está inscrita en el piloto clínico activo. No viaja en ningún claim del token:
/// este es el único punto del contrato donde el cliente puede conocerlo.
/// </param>
public sealed record AuthenticatedUser(
    Guid UserId,
    string KeycloakId,
    string Email,
    string Role,
    string FullName,
    bool EmailVerified,
    bool IsInActivePilot);
