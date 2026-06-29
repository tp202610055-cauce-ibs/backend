using Cauce.Domain.Common.Exceptions;

namespace Cauce.Domain.ClinicalRegistry.Exceptions;

/// <summary>
/// Se lanza cuando se referencia un síntoma que no existe.
/// </summary>
public sealed class SymptomNotFoundException : DomainException
{
    /// <summary>
    /// Identificador del síntoma no encontrado.
    /// </summary>
    public Guid SymptomId { get; }

    /// <summary>
    /// Inicializa la excepción con el identificador del síntoma.
    /// </summary>
    /// <param name="symptomId">Identificador del síntoma.</param>
    public SymptomNotFoundException(Guid symptomId)
        : base($"El síntoma {symptomId} no existe.")
    {
        SymptomId = symptomId;
    }
}
