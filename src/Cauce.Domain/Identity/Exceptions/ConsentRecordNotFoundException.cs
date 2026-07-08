using Cauce.Domain.Common.Exceptions;

namespace Cauce.Domain.Identity.Exceptions;

/// <summary>
/// Se lanza cuando un paciente solicita el comprobante de su consentimiento pero no existe un registro
/// de consentimiento vigente asociado a su cuenta (US01 CA04).
/// </summary>
public sealed class ConsentRecordNotFoundException : DomainException
{
    /// <summary>
    /// Inicializa la excepción con el mensaje estándar.
    /// </summary>
    public ConsentRecordNotFoundException()
        : base("No se encontró un registro de consentimiento vigente para la cuenta.")
    {
    }
}
