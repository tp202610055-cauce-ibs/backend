using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cauce.Api.IntegrationTests.Identity.Support;
using FluentAssertions;

namespace Cauce.Api.IntegrationTests.Identity;

/// <summary>
/// Pruebas del endpoint público del consentimiento vigente (US01). Verifican que sea accesible sin
/// autenticación, que el hash publicado corresponda al texto publicado y que ambos sean estables
/// entre llamadas. Requieren Docker (PostgreSQL + Redis).
/// </summary>
[Trait("Category", "Integration")]
public sealed class ConsentEndpointTests
    : Prompt5IntegrationTestBase, IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>
{
    private const string CurrentConsentUrl = "/api/v1/consent/current";

    /// <summary>
    /// Inicializa la prueba con los fixtures compartidos.
    /// </summary>
    /// <param name="postgres">Fixture del contenedor PostgreSQL.</param>
    /// <param name="redis">Fixture del contenedor Redis/KeyDB.</param>
    public ConsentEndpointTests(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis)
    {
    }

    [SkippableFact]
    public async Task GetCurrent_Anonymous_ReturnsVersionTextAndHash()
    {
        SkipIfUnavailable();
        var client = Factory.CreateClient();

        var response = await client.GetAsync(CurrentConsentUrl);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var (version, text, hash) = await ConsentAsync(response);

        version.Should().Be(CustomWebApplicationFactory.ConsentVersion);
        text.Should().Be(CustomWebApplicationFactory.ConsentText);
        hash.Should().HaveLength(64).And.MatchRegex("^[0-9a-f]{64}$");
    }

    [SkippableFact]
    public async Task GetCurrent_HashMatchesLocalSha256OfText()
    {
        SkipIfUnavailable();
        var client = Factory.CreateClient();

        var response = await client.GetAsync(CurrentConsentUrl);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var (_, text, hash) = await ConsentAsync(response);

        // Invariante contractual: el cliente debe poder recalcular el hash del texto publicado y
        // obtener exactamente el mismo valor que el backend compara en el registro.
        var expected = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
        hash.Should().Be(expected);
    }

    [SkippableFact]
    public async Task GetCurrent_RepeatedCalls_ReturnStableValues()
    {
        SkipIfUnavailable();
        var client = Factory.CreateClient();

        var first = await ConsentAsync(await client.GetAsync(CurrentConsentUrl));
        var second = await ConsentAsync(await client.GetAsync(CurrentConsentUrl));

        second.Version.Should().Be(first.Version);
        second.Hash.Should().Be(first.Hash);
        second.Text.Should().Be(first.Text);
    }

    /// <summary>
    /// Lee el cuerpo del consentimiento verificando de paso que las propiedades salgan en camelCase,
    /// tal como las serializa la política de nombres configurada en <c>Program.cs</c>.
    /// </summary>
    /// <param name="response">Respuesta HTTP a inspeccionar.</param>
    /// <returns>La versión, el texto y el hash publicados.</returns>
    private static async Task<(string Version, string Text, string Hash)> ConsentAsync(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;

        root.TryGetProperty("version", out var version).Should().BeTrue();
        root.TryGetProperty("text", out var text).Should().BeTrue();
        root.TryGetProperty("hash", out var hash).Should().BeTrue();

        return (version.GetString()!, text.GetString()!, hash.GetString()!);
    }
}
