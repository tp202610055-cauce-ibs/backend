using Cauce.Domain.Common.Exceptions;

namespace Cauce.Domain.Identity.Exceptions;

/// <summary>
/// Se lanza cuando el token de restablecimiento de contraseña no existe, ya fue
/// usado o es inválido por cualquier otra razón distinta de la expiración.
/// </summary>
public sealed class InvalidPasswordResetTokenException : DomainException
{
    /// <summary>
    /// Inicializa la excepción con el mensaje estándar.
    /// </summary>
    public InvalidPasswordResetTokenException()
        : base("El enlace de restablecimiento de contraseña no es válido.")
    {
    }
}
