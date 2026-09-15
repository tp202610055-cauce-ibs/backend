using Testcontainers.Minio;

namespace Cauce.Api.IntegrationTests.Identity.Support;

/// <summary>
/// Fixture que levanta un contenedor MinIO **efímero** (propio de la suite, no el stack de
/// desarrollo) para las pruebas del flujo de reportes. Si Docker no está disponible, marca el fixture
/// como no disponible para que las pruebas se omitan en vez de fallar.
/// </summary>
public sealed class MinioFixture : IAsyncLifetime
{
    private MinioContainer? _container;

    /// <summary>
    /// Indica si el contenedor está disponible.
    /// </summary>
    public bool IsAvailable { get; private set; }

    /// <summary>
    /// Endpoint del contenedor en formato <c>host:puerto</c> (sin esquema), válido si
    /// <see cref="IsAvailable"/> es verdadero.
    /// </summary>
    public string Endpoint { get; private set; } = string.Empty;

    /// <summary>
    /// Clave de acceso del contenedor.
    /// </summary>
    public string AccessKey { get; private set; } = string.Empty;

    /// <summary>
    /// Clave secreta del contenedor.
    /// </summary>
    public string SecretKey { get; private set; } = string.Empty;

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        try
        {
            // Imagen fijada al fork comunitario pgsty/minio. La oficial minio/minio
            // fue retirada del registry y el pull falla, lo que dejaba este fixture
            // como no disponible y omitia en silencio las pruebas de MinIO. Tag
            // explicito y nunca :latest, para que la suite sea reproducible.
            _container = new MinioBuilder()
                .WithImage("pgsty/minio:RELEASE.2026-08-04T00-00-00Z")
                .Build();
            await _container.StartAsync();

            Endpoint = _container.GetConnectionString()
                .Replace("http://", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("https://", string.Empty, StringComparison.OrdinalIgnoreCase);
            AccessKey = _container.GetAccessKey();
            SecretKey = _container.GetSecretKey();
            IsAvailable = true;
        }
        catch (Exception)
        {
            IsAvailable = false;
        }
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}
