using Cauce.Domain.Common.Exceptions;

namespace Cauce.Domain.Identity.Exceptions;

/// <summary>
/// Se lanza cuando el token de restablecimiento de contraseña superó su ventana
/// de vigencia de 30 minutos.
/// </summary>
public sealed class ExpiredPasswordResetTokenException : DomainException
{
    /// <summary>
    /// Inicializa la excepción con el mensaje estándar.
    /// </summary>
    public ExpiredPasswordResetTokenException()
        : base("El enlace de restablecimiento de contraseña ha expirado.")
    {
    }
}
