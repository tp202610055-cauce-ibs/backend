namespace Cauce.Infrastructure.Reports;

/// <summary>
/// Configuración de la generación de reportes clínicos. Se vincula a la sección <c>Reports</c>.
/// </summary>
public sealed class ReportOptions
{
    /// <summary>
    /// Nombre de la sección de configuración asociada a estas opciones.
    /// </summary>
    public const string SectionName = "Reports";

    /// <summary>
    /// Validez, en horas, de la URL prefirmada de descarga del reporte.
    /// </summary>
    public int PresignedUrlValidityHours { get; init; } = 24;

    /// <summary>
    /// Longitud de la contraseña generada para cifrar el PDF.
    /// </summary>
    public int PasswordLength { get; init; } = 12;

    /// <summary>
    /// Cantidad máxima de alimentos frecuentes a incluir en el reporte.
    /// </summary>
    public int TopFrequentFoods { get; init; } = 10;
}
