using Cauce.Domain.Common.Exceptions;

namespace Cauce.Domain.Identity.Exceptions;

/// <summary>
/// Se lanza cuando se intenta operar sobre una cuenta bloqueada temporalmente por
/// intentos fallidos consecutivos.
/// </summary>
public sealed class AccountLockedException : DomainException
{
    /// <summary>
    /// Momento, en UTC, hasta el cual la cuenta permanece bloqueada.
    /// </summary>
    public DateTime LockedUntil { get; }

    /// <summary>
    /// Inicializa la excepción con el instante de desbloqueo.
    /// </summary>
    /// <param name="lockedUntil">Momento UTC hasta el cual la cuenta está bloqueada.</param>
    public AccountLockedException(DateTime lockedUntil)
        : base("La cuenta se encuentra bloqueada temporalmente por intentos fallidos consecutivos.")
    {
        LockedUntil = lockedUntil;
    }
}
