using Testcontainers.PostgreSql;

namespace Cauce.Api.IntegrationTests.Identity.Support;

/// <summary>
/// Fixture que levanta un contenedor PostgreSQL real para las pruebas de
/// integración. Si Docker no está disponible, marca el fixture como no disponible
/// para que las pruebas se omitan en lugar de fallar.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

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
        if (IsAvailable)
        {
            await _container.DisposeAsync();
        }
    }
}
