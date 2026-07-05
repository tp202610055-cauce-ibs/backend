using Cauce.Domain.Common.Exceptions;

namespace Cauce.Domain.Reports.Exceptions;

/// <summary>
/// Se lanza cuando un nutricionista intenta generar un reporte de un paciente que no tiene
/// asignado. No revela información del recurso.
/// </summary>
public sealed class ReportAccessDeniedException : DomainException
{
    /// <summary>
    /// Inicializa la excepción con su mensaje estándar.
    /// </summary>
    public ReportAccessDeniedException()
        : base("No tiene autorización para generar reportes de este paciente.")
    {
    }
}
