using Testcontainers.Redis;

namespace Cauce.Api.IntegrationTests.Identity.Support;

/// <summary>
/// Fixture que levanta un contenedor Redis (compatible con KeyDB) real para las pruebas
/// de integración de idempotencia. Si Docker no está disponible, marca el fixture como no
/// disponible para que las pruebas se omitan en lugar de fallar. Sigue el mismo patrón que
/// <see cref="PostgresFixture"/>.
/// </summary>
public sealed class RedisFixture : IAsyncLifetime
{
    private RedisContainer? _container;

    /// <summary>
    /// Indica si el contenedor está disponible (Docker presente y arranque exitoso).
    /// </summary>
    public bool IsAvailable { get; private set; }

    /// <summary>
    /// Cadena de conexión al contenedor; válida solo si <see cref="IsAvailable"/> es verdadero.
    /// </summary>
    public string ConnectionString { get; private set; } = string.Empty;

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        try
        {
            _container = new RedisBuilder()
                .WithImage("redis:7-alpine")
                .Build();
            await _container.StartAsync();
            ConnectionString = _container.GetConnectionString();
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
