namespace Cauce.Api.Application.Exceptions;

/// <summary>
/// Se lanza cuando una cuenta está bloqueada temporalmente por superar el
/// umbral de intentos fallidos consecutivos (US05 CA02: 5 intentos).
/// </summary>
public class AccountLockedException : Exception
{
    /// <summary>Momento UTC en que la cuenta se desbloqueará automáticamente.</summary>
    public DateTimeOffset LockedUntil { get; }

    public AccountLockedException(DateTimeOffset lockedUntil)
        : base($"La cuenta está bloqueada temporalmente por intentos fallidos. Vuelva a intentarlo después de {lockedUntil:u}.")
    {
        LockedUntil = lockedUntil;
    }
}