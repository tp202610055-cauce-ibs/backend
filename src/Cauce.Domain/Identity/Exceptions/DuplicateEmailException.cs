using Cauce.Domain.Common.Exceptions;

namespace Cauce.Domain.Identity.Exceptions;

/// <summary>
/// Se lanza cuando se intenta registrar una cuenta con una dirección de correo
/// electrónico que ya existe en el sistema.
/// </summary>
public sealed class DuplicateEmailException : DomainException
{
    /// <summary>
    /// Inicializa la excepción con el mensaje estándar, sin exponer el correo.
    /// </summary>
    public DuplicateEmailException()
        : base("Ya existe una cuenta registrada con esta dirección de correo electrónico.")
    {
    }
}
