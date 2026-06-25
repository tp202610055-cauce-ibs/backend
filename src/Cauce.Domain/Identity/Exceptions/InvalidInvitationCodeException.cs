using Cauce.Domain.Common.Exceptions;

namespace Cauce.Domain.Identity.Exceptions;

/// <summary>
/// Se lanza cuando el código de invitación proporcionado no existe o no es válido.
/// </summary>
public sealed class InvalidInvitationCodeException : DomainException
{
    /// <summary>
    /// Inicializa la excepción con el mensaje estándar.
    /// </summary>
    public InvalidInvitationCodeException()
        : base("El código de invitación proporcionado no es válido.")
    {
    }
}
