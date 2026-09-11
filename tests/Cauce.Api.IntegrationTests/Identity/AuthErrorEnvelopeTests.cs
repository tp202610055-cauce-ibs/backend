using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Cauce.Api.IntegrationTests.Identity.Support;
using FluentAssertions;

namespace Cauce.Api.IntegrationTests.Identity;

/// <summary>
/// Pruebas del envelope de error de <c>POST /api/v1/auth/login</c> en sus cuatro formas: el 400 del
/// binding de <c>[ApiController]</c>, el 400 de validación de dominio, el 401 de credenciales y el 429
/// del limitador de tasa. El cliente móvil tiene que manejar las cuatro, y dos de ellas no traen
/// <c>errorCode</c>. Requieren Docker (PostgreSQL + Redis).
/// </summary>
[Trait("Category", "Integration")]
public sealed class AuthErrorEnvelopeTests
    : IntegrationTestBase, IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>
{
    private const string LoginUrl = "/api/v1/auth/login";

    /// <summary>
    /// Inicializa la prueba con los fixtures compartidos.
    /// </summary>
    /// <param name="postgres">Fixture del contenedor PostgreSQL.</param>
    /// <param name="redis">Fixture del contenedor Redis/KeyDB.</param>
    public AuthErrorEnvelopeTests(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis)
    {
    }

    [SkippableFact]
    public async Task Login_MalformedJson_Returns400WithoutErrorCode()
    {
        SkipIfUnavailable();
        var client = Factory.CreateClient();

        var response = await client.PostAsync(
            LoginUrl,
            new StringContent("{ esto no es json valido", Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        // El 400 automático de MVC no pasa por el middleware, así que no trae la extensión errorCode.
        // El cliente debe tratarla como opcional y apoyarse en `errors`.
        document.RootElement.TryGetProperty("errorCode", out _).Should().BeFalse();
        document.RootElement.TryGetProperty("errors", out _).Should().BeTrue();
    }

    [SkippableFact]
    public async Task Login_MissingRequiredField_Returns400WithCamelCaseValidationErrors()
    {
        SkipIfUnavailable();
        var client = Factory.CreateClient();

        var response = await client.PostAsJsonAsync(LoginUrl, new
        {
            email = "paciente@cauce.local",
            password = string.Empty,
            clientId = "cauce-mobile"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("errorCode").GetString().Should().Be("validation_error");

        var errors = document.RootElement.GetProperty("errors");
        errors.TryGetProperty("password", out _).Should().BeTrue("las claves salen en camelCase");
        errors.TryGetProperty("Password", out _).Should().BeFalse();
    }

    [SkippableFact]
    public async Task Login_InvalidCredentials_Returns401WithGenericErrorCode()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        var client = Factory.CreateClient();

        var response = await client.PostAsJsonAsync(LoginUrl, new
        {
            email = patient.Email,
            password = "ContrasenaIncorrecta1",
            clientId = "cauce-mobile"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("errorCode").GetString().Should().Be("invalid_credentials");
        document.RootElement.GetProperty("status").GetInt32().Should().Be(401);
    }

    [SkippableFact]
    public async Task Login_RateLimited_Returns429WithRetryAfterSeconds()
    {
        SkipIfUnavailable();
        var client = Factory.CreateClient();
        var payload = new { email = "alguien@cauce.local", password = "x", clientId = "cauce-mobile" };

        HttpResponseMessage? limited = null;
        // La política auth-login permite 10 por minuto y particiona por IP; en el servidor de pruebas
        // todas las peticiones caen en la misma partición.
        for (var attempt = 1; attempt <= 12 && limited is null; attempt++)
        {
            var response = await client.PostAsJsonAsync(LoginUrl, payload);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                limited = response;
            }
        }

        limited.Should().NotBeNull("tras superar el umbral debe responder 429");

        using var document = JsonDocument.Parse(await limited!.Content.ReadAsStringAsync());
        var root = document.RootElement;
        root.GetProperty("status").GetInt32().Should().Be(429);
        root.TryGetProperty("retryAfterSeconds", out _).Should().BeTrue();
        // El 429 lo construye el limitador, no el middleware: tampoco trae errorCode.
        root.TryGetProperty("errorCode", out _).Should().BeFalse();
    }
}
