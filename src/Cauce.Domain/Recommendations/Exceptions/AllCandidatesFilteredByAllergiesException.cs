using Cauce.Domain.Common.Exceptions;

namespace Cauce.Domain.Recommendations.Exceptions;

/// <summary>
/// Se lanza cuando todos los alimentos candidatos del paciente quedan excluidos por el
/// filtro de alergias, dejando el conjunto de candidatos vacío.
/// </summary>
public sealed class AllCandidatesFilteredByAllergiesException : DomainException
{
    /// <summary>
    /// Inicializa la excepción con el identificador del paciente.
    /// </summary>
    /// <param name="patientId">Identificador del paciente.</param>
    public AllCandidatesFilteredByAllergiesException(Guid patientId)
        : base($"Todos los alimentos candidatos del paciente '{patientId}' fueron excluidos por sus alergias declaradas.")
    {
        PatientId = patientId;
    }

    /// <summary>
    /// Identificador del paciente cuyos candidatos fueron filtrados.
    /// </summary>
    public Guid PatientId { get; }
}
