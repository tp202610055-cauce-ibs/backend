namespace Cauce.Infrastructure.Storage;

/// <summary>
/// Configuración de conexión al almacenamiento de objetos MinIO (S3-compatible). Se vincula a la
/// sección <c>Storage:Minio</c> de la configuración.
/// </summary>
public sealed class MinioOptions
{
    /// <summary>
    /// Nombre de la sección de configuración asociada a estas opciones.
    /// </summary>
    public const string SectionName = "Storage:Minio";

    /// <summary>
    /// Endpoint del servidor MinIO, por ejemplo <c>localhost:9000</c> (sin esquema).
    /// </summary>
    public required string Endpoint { get; init; }

    /// <summary>
    /// Clave de acceso.
    /// </summary>
    public required string AccessKey { get; init; }

    /// <summary>
    /// Clave secreta. Se provee vía User Secrets o variables de entorno.
    /// </summary>
    public required string SecretKey { get; init; }

    /// <summary>
    /// Indica si la conexión usa TLS. Es <see langword="false"/> en desarrollo.
    /// </summary>
    public bool UseSsl { get; init; }

    /// <summary>
    /// Nombre del bucket de los reportes clínicos.
    /// </summary>
    public string ReportsBucket { get; init; } = "clinical-reports";

    /// <summary>
    /// Nombre del bucket de las exportaciones de portabilidad de datos del paciente (US25).
    /// </summary>
    public string ExportsBucket { get; init; } = "patient-exports";
}
