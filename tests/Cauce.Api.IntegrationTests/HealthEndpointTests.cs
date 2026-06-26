using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Cauce.Api.IntegrationTests;

/// <summary>
/// Pruebas de integración del endpoint de salud. Levantan la API completa en
/// memoria con <see cref="WebApplicationFactory{TEntryPoint}"/> y verifican que
/// el endpoint público responde correctamente.
/// </summary>
[Trait("Category", "Integration")]
public sealed class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    /// <summary>
    /// Inicializa la prueba con la fábrica de la aplicación web.
    /// </summary>
    /// <param name="factory">Fábrica que levanta la API en memoria.</param>
    public HealthEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Verifica que <c>GET /api/v1/health</c> responde 200 OK, con cuerpo que
    /// indica estado saludable y tipo de contenido JSON.
    /// </summary>
    [Fact]
    public async Task Get_HealthEndpoint_Returns200WithHealthyStatus()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/v1/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("healthy");

        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
    }
}
