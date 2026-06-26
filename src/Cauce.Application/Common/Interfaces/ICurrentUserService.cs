namespace Cauce.Application.Common.Interfaces;

/// <summary>
/// Expone la información del usuario autenticado en la petición actual, derivada
/// del JWT emitido por Keycloak y del contexto HTTP. Cuando no hay petición HTTP
/// (por ejemplo, en un worker en segundo plano), todas las propiedades devuelven
/// <see langword="null"/> y <see cref="IsAuthenticated"/> es <see langword="false"/>.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>
    /// Identificador del usuario extraído del claim <c>sub</c> del JWT. Corresponde
    /// al identificador de Keycloak, no a la clave primaria de la tabla <c>users</c>.
    /// </summary>
    Guid? UserId { get; }

    /// <summary>
    /// Correo electrónico extraído del claim <c>email</c>.
    /// </summary>
    string? Email { get; }

    /// <summary>
    /// Roles del usuario extraídos del claim <c>realm_access.roles</c>.
    /// </summary>
    IReadOnlyList<string> Roles { get; }

    /// <summary>
    /// Indica si la petición actual está autenticada con un JWT válido.
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// Dirección IP de origen de la petición.
    /// </summary>
    string? IpAddress { get; }

    /// <summary>
    /// User agent informado en la cabecera <c>User-Agent</c> de la petición.
    /// </summary>
    string? UserAgent { get; }
}
