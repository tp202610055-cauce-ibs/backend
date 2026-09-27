using System.Net;
using Cauce.Api.IntegrationTests.Identity.Support;
using FluentAssertions;

namespace Cauce.Api.IntegrationTests.Identity;

/// <summary>
/// Pruebas de CORS para el portal (acta A68): el origen configurado puede renovar la sesión con la cookie
/// y el header propio del portal, y cualquier otro origen queda fuera. Requieren Docker (PostgreSQL + Redis).
/// </summary>
[Trait("Category", "Integration")]
public sealed class PortalCorsTests
    : IntegrationTestBase, IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>
{
    /// <summary>
    /// Inicializa la prueba con los fixtures compartidos.
    /// </summary>
    /// <param name="postgres">Fixture del contenedor PostgreSQL.</param>
    /// <param name="redis">Fixture del contenedor Redis/KeyDB.</param>
    public PortalCorsTests(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis)
    {
    }

    [SkippableFact]
    public async Task Preflight_FromThePortalOrigin_AllowsTheCsrfHeaderAndCredentials()
    {
        SkipIfUnavailable();

        var response = await PreflightAsync("http://localhost:5173");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        response.Headers.GetValues("Access-Control-Allow-Origin").Should().Equal("http://localhost:5173");
        response.Headers.GetValues("Access-Control-Allow-Credentials").Should().Equal("true");
        string.Join(",", response.Headers.GetValues("Access-Control-Allow-Headers"))
            .Should().ContainEquivalentOf("x-cauce-portal");
    }

    [SkippableFact]
    public async Task Preflight_FromAnotherOrigin_IsNotAllowed()
    {
        SkipIfUnavailable();

        // El puerto 3000 salió de la lista: nada lo usa (acta A68).
        var response = await PreflightAsync("http://localhost:3000");

        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }

    private async Task<HttpResponseMessage> PreflightAsync(string origin)
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/auth/portal/refresh");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "x-cauce-portal");
        return await Factory.CreateClient().SendAsync(request);
    }
}
