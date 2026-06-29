using Cauce.Domain.Common.Exceptions;

namespace Cauce.Domain.ClinicalRegistry.Exceptions;

/// <summary>
/// Se lanza cuando un paciente intenta crear un alimento personalizado con un nombre
/// que ya tiene registrado.
/// </summary>
public sealed class DuplicateCustomFoodException : DomainException
{
    /// <summary>
    /// Inicializa la excepción.
    /// </summary>
    public DuplicateCustomFoodException()
        : base("Ya existe un alimento personalizado con ese nombre para el paciente.")
    {
    }
}
