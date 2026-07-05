using Cauce.Domain.Common.Exceptions;

namespace Cauce.Domain.Reports.Exceptions;

/// <summary>
/// Se lanza cuando el período solicitado para un reporte clínico es inválido.
/// </summary>
public sealed class ReportPeriodInvalidException : DomainException
{
    /// <summary>
    /// Inicializa la excepción con el motivo de la invalidez.
    /// </summary>
    /// <param name="reason">Descripción del período inválido.</param>
    public ReportPeriodInvalidException(string reason)
        : base($"El período del reporte es inválido: {reason}")
    {
    }
}
