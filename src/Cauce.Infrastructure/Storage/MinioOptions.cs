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
    /// Endpoint con el que se firman las URLs prefirmadas de descarga, por ejemplo
    /// <c>minio.hospital.local</c> (sin esquema). Si queda vacío se usa <see cref="Endpoint"/>.
    ///
    /// <para>Existe porque el host con el que el backend habla con MinIO no tiene por qué ser el
    /// host desde el que el paciente descarga. Dentro de la red de Docker el backend resuelve
    /// <c>minio:9000</c>, un nombre que no existe fuera del contenedor: una URL firmada contra ese
    /// host no abre en el celular. Y no alcanza con reescribir el host de la URL después de
    /// firmarla, porque el host forma parte de lo que la firma cubre; hay que firmar directamente
    /// contra el host público.</para>
    /// </summary>
    public string? PublicEndpoint { get; init; }

    /// <summary>
    /// Indica si el endpoint público usa TLS. Solo aplica cuando <see cref="PublicEndpoint"/> tiene
    /// valor; el caso normal en producción es <see langword="true"/> aunque la conexión interna no lo
    /// use.
    /// </summary>
    public bool PublicUseSsl { get; init; }

    /// <summary>
    /// Indica si hay un endpoint público distinto del de conexión.
    /// </summary>
    public bool HasSeparatePublicEndpoint =>
        !string.IsNullOrWhiteSpace(PublicEndpoint)
        && !string.Equals(PublicEndpoint, Endpoint, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Nombre del bucket de los reportes clínicos.
    /// </summary>
    public string ReportsBucket { get; init; } = "clinical-reports";

    /// <summary>
    /// Nombre del bucket de las exportaciones de portabilidad de datos del paciente (US25).
    /// </summary>
    public string ExportsBucket { get; init; } = "patient-exports";
}
