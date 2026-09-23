using Minio;

namespace Cauce.Infrastructure.Storage;

/// <summary>
/// Cliente de MinIO dedicado a firmar las URLs de descarga. Envuelve un <see cref="IMinioClient"/>
/// apuntado al endpoint <b>público</b> (<c>Storage:Minio:PublicEndpoint</c>) en lugar del endpoint de
/// conexión interna.
///
/// <para>Es un tipo propio y no una segunda registración de <see cref="IMinioClient"/> para que la
/// inyección no dependa del orden de registro: quien necesita firmar pide este tipo, quien necesita
/// hablar con MinIO pide <see cref="IMinioClient"/>, y no hay forma de confundirlos. Cuando no hay
/// endpoint público configurado, envuelve el mismo cliente de conexión y el comportamiento es el
/// anterior al cambio.</para>
/// </summary>
public sealed class MinioPresignClient
{
    /// <summary>
    /// Cliente con el que se generan las firmas.
    /// </summary>
    public IMinioClient Client { get; }

    /// <summary>
    /// Inicializa el cliente de firma.
    /// </summary>
    /// <param name="client">Cliente de MinIO apuntado al endpoint de firma.</param>
    public MinioPresignClient(IMinioClient client)
    {
        Client = client;
    }
}
