using Cauce.Domain.Common.Exceptions;

namespace Cauce.Domain.Notifications.Exceptions;

/// <summary>
/// Se lanza cuando el aviso no existe o no pertenece al nutricionista que lo pide.
/// </summary>
public sealed class NutritionistAlertNotFoundException : DomainException
{
    /// <summary>
    /// Inicializa la excepción.
    /// </summary>
    public NutritionistAlertNotFoundException()
        : base("El aviso no existe o no pertenece a tu bandeja.")
    {
    }
}
