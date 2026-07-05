namespace Cauce.Application.Common.Interfaces.Storage;

/// <summary>
/// Almacenamiento de objetos S3-compatible (MinIO) para artefactos como los reportes clínicos.
/// </summary>
public interface IObjectStorage
{
    /// <summary>
    /// Sube un objeto y devuelve su clave.
    /// </summary>
    /// <param name="bucket">Bucket de destino.</param>
    /// <param name="objectKey">Clave del objeto.</param>
    /// <param name="content">Contenido del objeto.</param>
    /// <param name="contentType">Tipo MIME del contenido.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>La clave del objeto subido.</returns>
    Task<string> UploadAsync(string bucket, string objectKey, Stream content, string contentType, CancellationToken ct = default);

    /// <summary>
    /// Genera una URL prefirmada de descarga con la validez indicada.
    /// </summary>
    /// <param name="bucket">Bucket del objeto.</param>
    /// <param name="objectKey">Clave del objeto.</param>
    /// <param name="validity">Ventana de validez de la URL.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>La URL prefirmada.</returns>
    Task<string> GetPresignedUrlAsync(string bucket, string objectKey, TimeSpan validity, CancellationToken ct = default);

    /// <summary>
    /// Asegura, de forma idempotente, que el bucket exista.
    /// </summary>
    /// <param name="bucket">Nombre del bucket.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Tarea que representa la operación asíncrona.</returns>
    Task EnsureBucketExistsAsync(string bucket, CancellationToken ct = default);
}
