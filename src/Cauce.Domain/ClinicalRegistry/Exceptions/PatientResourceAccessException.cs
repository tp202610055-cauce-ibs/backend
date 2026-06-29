using Cauce.Domain.Common.Exceptions;

namespace Cauce.Domain.ClinicalRegistry.Exceptions;

/// <summary>
/// Se lanza cuando un paciente intenta acceder a un recurso clínico (comida, síntoma,
/// alimento personalizado, nota o evaluación) que no le pertenece.
/// </summary>
public sealed class PatientResourceAccessException : DomainException
{
    /// <summary>
    /// Inicializa la excepción.
    /// </summary>
    public PatientResourceAccessException()
        : base("El recurso solicitado no pertenece al paciente autenticado.")
    {
    }
}
