using Cauce.Domain.Common.Exceptions;

namespace Cauce.Domain.Identity.Exceptions;

/// <summary>
/// Se lanza cuando el refresh token presentado no permite renovar la sesión: expiró, fue revocado por
/// un cierre de sesión, o ya se consumió (el realm tiene rotación activada, así que cada refresh
/// invalida el token anterior).
/// </summary>
public sealed class InvalidRefreshTokenException : DomainException
{
    /// <summary>
    /// Inicializa la excepción con su mensaje canónico.
    /// </summary>
    public InvalidRefreshTokenException()
        : base("El token de refresco no es válido o ya expiró. Vuelva a iniciar sesión.")
    {
    }
}
