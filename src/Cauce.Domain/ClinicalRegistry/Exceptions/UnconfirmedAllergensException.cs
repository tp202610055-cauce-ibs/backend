using Cauce.Domain.Common.Exceptions;

namespace Cauce.Domain.ClinicalRegistry.Exceptions;

/// <summary>
/// Se lanza cuando un paciente intenta crear un alimento personalizado cuyos ingredientes coinciden
/// con una o más alergias declaradas y aún no confirmó explícitamente que desea continuar (US10 CA03).
/// Transporta el detalle de las coincidencias para devolverlo al cliente con código 409.
/// </summary>
public sealed class UnconfirmedAllergensException : DomainException
{
    /// <summary>
    /// Coincidencias detectadas entre los ingredientes y las alergias del paciente.
    /// </summary>
    public IReadOnlyList<DetectedAllergen> DetectedAllergens { get; }

    /// <summary>
    /// Inicializa la excepción con las coincidencias detectadas.
    /// </summary>
    /// <param name="detectedAllergens">Coincidencias alérgeno-ingrediente detectadas.</param>
    public UnconfirmedAllergensException(IReadOnlyList<DetectedAllergen> detectedAllergens)
        : base("El alimento personalizado contiene ingredientes que coinciden con alergias declaradas del paciente. Se requiere confirmación explícita para continuar.")
    {
        DetectedAllergens = detectedAllergens;
    }
}
