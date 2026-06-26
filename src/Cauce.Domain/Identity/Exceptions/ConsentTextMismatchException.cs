using Cauce.Domain.Common.Exceptions;

namespace Cauce.Domain.Identity.Exceptions;

/// <summary>
/// Se lanza cuando el hash del texto de consentimiento enviado por el cliente no
/// coincide con el que el backend calcula para la versión declarada del documento.
/// </summary>
public sealed class ConsentTextMismatchException : DomainException
{
    /// <summary>
    /// Inicializa la excepción con el mensaje estándar.
    /// </summary>
    public ConsentTextMismatchException()
        : base("El texto de consentimiento aceptado no coincide con la versión vigente del documento.")
    {
    }
}
