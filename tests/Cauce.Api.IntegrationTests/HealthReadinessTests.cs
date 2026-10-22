using System.Net;
using Cauce.Api.IntegrationTests.Identity.Support;
using FluentAssertions;

namespace Cauce.Api.IntegrationTests;

/// <summary>
/// Pruebas de <c>/api/v1/health/ready</c>, que comprueba la base de datos, frente a <c>/api/v1/health/live</c>,
/// que no depende de ella.
/// </summary>
[Trait("Category", "Integration")]
public sealed class HealthReadinessTests : IClassFixture<PostgresFixture>
{
    private const string UnreachableDatabase = "Host=127.0.0.1;Port=1;Database=cauce;Username=cauce;Password=x;Timeout=2";

    private readonly PostgresFixture _postgres;

    /// <summary>
    /// Inicializa la prueba con el fixture de PostgreSQL.
    /// </summary>
    public HealthReadinessTests(PostgresFixture postgres)
    {
        _postgres = postgres;
    }

    [SkippableFact]
    public async Task Ready_WithTheDatabaseUp_Returns200()
    {
        Skip.IfNot(_postgres.IsAvailable, "Docker no está disponible; se omite.");
        await using var factory = new CustomWebApplicationFactory(_postgres.ConnectionString);

        var response = await factory.CreateClient().GetAsync("/api/v1/health/ready");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("Healthy");
    }

    [Fact]
    public async Task Ready_WithTheDatabaseDown_Returns503AndLiveStays200()
    {
        await using var factory = new CustomWebApplicationFactory(UnreachableDatabase);
        var client = factory.CreateClient();

        var ready = await client.GetAsync("/api/v1/health/ready");
        var live = await client.GetAsync("/api/v1/health/live");

        ready.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        live.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
