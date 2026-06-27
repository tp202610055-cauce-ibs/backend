using Cauce.Domain.Common.Exceptions;

namespace Cauce.Domain.Patients.Exceptions;

/// <summary>
/// Se lanza cuando se intenta crear una asignación nutricionista-paciente que ya
/// existe de forma activa.
/// </summary>
public sealed class NutritionistAssignmentAlreadyExistsException : DomainException
{
    /// <summary>
    /// Inicializa la excepción con el mensaje estándar.
    /// </summary>
    public NutritionistAssignmentAlreadyExistsException()
        : base("Ya existe una asignación activa entre el nutricionista y el paciente.")
    {
    }
}
