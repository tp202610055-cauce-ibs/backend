using Testcontainers.PostgreSql;

namespace Cauce.Api.IntegrationTests.Identity.Support;

/// <summary>
/// Fixture que levanta un contenedor PostgreSQL real para las pruebas de
/// integración. Si Docker no está disponible (o su endpoint no se puede resolver),
/// marca el fixture como no disponible para que las pruebas se omitan en lugar de
/// fallar. La construcción del contenedor se hace dentro de <see cref="InitializeAsync"/>
/// para que un endpoint mal configurado no rompa el constructor del fixture.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;

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
            _container = new PostgreSqlBuilder()
                .WithImage("postgres:16-alpine")
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
