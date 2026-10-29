using Cauce.Domain.Common.Exceptions;

namespace Cauce.Domain.Identity.Exceptions;

/// <summary>
/// Se lanza cuando una operación enviaría datos del paciente a un correo que aún no verificó. Evita que esos datos
/// lleguen a un tercero si el correo se escribió mal o no es suyo (Ley N.° 29733).
/// </summary>
public sealed class EmailNotVerifiedException : DomainException
{
    /// <summary>
    /// Inicializa la excepción con el mensaje estándar.
    /// </summary>
    public EmailNotVerifiedException()
        : base("Verifica tu correo para recibir tu reporte. Revisa tu bandeja o pide un nuevo enlace de verificación.")
    {
    }
}
