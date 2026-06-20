namespace Cauce.Api.Application.Exceptions;

/// <summary>
/// Se lanza cuando se intenta registrar un usuario con un correo
/// que ya existe en el sistema (US01 CA02).
/// </summary>
public class DuplicateEmailException : Exception
{
    public DuplicateEmailException(string email)
        : base($"El correo electrónico '{email}' ya está registrado en el sistema.") { }
}