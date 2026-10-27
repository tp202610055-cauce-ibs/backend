using Cauce.Domain.Common.Exceptions;

namespace Cauce.Domain.ClinicalRegistry.Exceptions;

/// <summary>
/// Se lanza cuando se pide agregar un término que ya está en el glosario, sin distinguir mayúsculas ni tildes.
/// </summary>
public sealed class GlossaryTermAlreadyExistsException : DomainException
{
    /// <summary>
    /// Inicializa la excepción.
    /// </summary>
    public GlossaryTermAlreadyExistsException()
        : base("El término ya está en el glosario.")
    {
    }
}
