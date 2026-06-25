using Cauce.Domain.Common.Exceptions;

namespace Cauce.Domain.Identity.Exceptions;

/// <summary>
/// Se lanza cuando el código de invitación superó su ventana de vigencia.
/// </summary>
public sealed class ExpiredInvitationCodeException : DomainException
{
    /// <summary>
    /// Inicializa la excepción con el mensaje estándar.
    /// </summary>
    public ExpiredInvitationCodeException()
        : base("El código de invitación ha expirado.")
    {
    }
}
