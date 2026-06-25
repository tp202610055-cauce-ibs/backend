using Cauce.Domain.Common.Exceptions;

namespace Cauce.Domain.Identity.Exceptions;

/// <summary>
/// Se lanza cuando se intenta consumir un código de invitación que ya fue usado.
/// </summary>
public sealed class InvitationCodeAlreadyUsedException : DomainException
{
    /// <summary>
    /// Inicializa la excepción con el mensaje estándar.
    /// </summary>
    public InvitationCodeAlreadyUsedException()
        : base("El código de invitación ya fue utilizado.")
    {
    }
}
