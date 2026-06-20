namespace Cauce.Api.Application.Exceptions;

/// <summary>
/// Se lanza cuando las credenciales de login son inválidas (US05 CA02).
/// El mensaje es genérico para no revelar si el correo existe o no en el sistema.
/// </summary>
public class InvalidCredentialsException : Exception
{
    public InvalidCredentialsException()
        : base("Credenciales inválidas. Verifique su correo y contraseña.") { }
}