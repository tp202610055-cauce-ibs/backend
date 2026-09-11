using Cauce.Domain.Common.Exceptions;
using Cauce.Domain.Identity.Enums;

namespace Cauce.Domain.Identity.Exceptions;

/// <summary>
/// Se lanza cuando se pide reenviar el enlace de activación a un nutricionista que ya no está pendiente
/// de activación (acta A52). Una cuenta activa ya definió su contraseña y, si la olvidó, tiene el
/// restablecimiento; una suspendida o dada de baja no debe recibir un enlace que le permita volver a entrar.
/// </summary>
public sealed class NutritionistNotPendingActivationException : DomainException
{
    /// <summary>
    /// Inicializa la excepción con el estado real de la cuenta.
    /// </summary>
    /// <param name="status">Estado de la cuenta del nutricionista.</param>
    public NutritionistNotPendingActivationException(UserStatus status)
        : base("El nutricionista no está pendiente de activación.")
    {
        Status = status;
    }

    /// <summary>
    /// Estado de la cuenta que impidió el reenvío.
    /// </summary>
    public UserStatus Status { get; }
}
