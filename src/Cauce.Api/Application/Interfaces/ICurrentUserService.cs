namespace Cauce.Api.Application.Interfaces;

/// <summary>
/// Abstracción para extraer información del usuario autenticado a partir
/// del contexto HTTP actual. Permite que los servicios de la capa Application
/// accedan a la identidad sin depender directamente del pipeline HTTP.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>
    /// Indica si la solicitud actual corresponde a un usuario autenticado.
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// Obtiene el identificador único del usuario autenticado a partir del claim "sub".
    /// </summary>
    /// <returns>El Guid del usuario autenticado.</returns>
    /// <exception cref="UnauthorizedAccessException">
    /// Se lanza si no hay un usuario autenticado o si el token no contiene un identificador válido.
    /// </exception>
    Guid GetUserId();
}