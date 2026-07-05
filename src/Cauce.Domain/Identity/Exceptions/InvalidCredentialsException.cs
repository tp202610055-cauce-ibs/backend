using Cauce.Domain.Common.Exceptions;

namespace Cauce.Domain.Identity.Exceptions;

/// <summary>
/// Se lanza cuando un inicio de sesión falla por credenciales inválidas. El mensaje es genérico:
/// no distingue entre "usuario no existe" y "contraseña incorrecta" para no filtrar la existencia
/// de cuentas.
/// </summary>
public sealed class InvalidCredentialsException : DomainException
{
    /// <summary>
    /// Inicializa la excepción con su mensaje genérico.
    /// </summary>
    public InvalidCredentialsException()
        : base("Credenciales inválidas.")
    {
    }
}
