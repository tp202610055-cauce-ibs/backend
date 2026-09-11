using Cauce.Domain.Common.Exceptions;

namespace Cauce.Domain.Identity.Exceptions;

/// <summary>
/// Se lanza cuando el identificador no corresponde a ninguna cuenta de nutricionista. Una cuenta de otro
/// rol responde igual que una inexistente, para no revelar qué identificadores existen.
/// </summary>
public sealed class NutritionistNotFoundException : DomainException
{
    /// <summary>
    /// Inicializa la excepción con el mensaje estándar.
    /// </summary>
    public NutritionistNotFoundException()
        : base("No existe un nutricionista con el identificador indicado.")
    {
    }
}
