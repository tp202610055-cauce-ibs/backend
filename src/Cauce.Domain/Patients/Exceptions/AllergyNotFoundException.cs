using Cauce.Domain.Common.Exceptions;

namespace Cauce.Domain.Patients.Exceptions;

/// <summary>
/// Se lanza cuando se referencia una alergia que no existe o no está activa en el
/// catálogo, o una declaración de alergia inexistente.
/// </summary>
public sealed class AllergyNotFoundException : DomainException
{
    /// <summary>
    /// Inicializa la excepción con el mensaje estándar.
    /// </summary>
    public AllergyNotFoundException()
        : base("La alergia indicada no existe o no está disponible.")
    {
    }
}
