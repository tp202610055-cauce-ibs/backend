using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cauce.Api.IntegrationTests.Identity.Support;
using FluentAssertions;

namespace Cauce.Api.IntegrationTests.Identity;

/// <summary>
/// Pruebas del envelope de error de validación (RFC 7807). Verifican que las claves del diccionario
/// <c>errors</c> salgan en camelCase, consistentes con el resto del contrato HTTP, que se serializa
/// con <see cref="System.Text.Json.JsonNamingPolicy.CamelCase"/>. Requieren Docker (PostgreSQL + Redis).
/// </summary>
[Trait("Category", "Integration")]
public sealed class ValidationErrorEnvelopeTests
    : Prompt5IntegrationTestBase, IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>
{
    private const string ValidConsentHash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    /// <summary>
    /// Inicializa la prueba con los fixtures compartidos.
    /// </summary>
    /// <param name="postgres">Fixture del contenedor PostgreSQL.</param>
    /// <param name="redis">Fixture del contenedor Redis/KeyDB.</param>
    public ValidationErrorEnvelopeTests(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis)
    {
    }

    [SkippableFact]
    public async Task RegisterPatient_MissingEmail_ReturnsCamelCaseErrorKey()
    {
        SkipIfUnavailable();
        var client = Factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/register", RegisterPayload(email: string.Empty));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errors = await ErrorsAsync(response);
        errors.Should().ContainKey("email");
        errors.Should().NotContainKey("Email");
    }

    [SkippableFact]
    public async Task RegisterPatient_MultipleFailures_ReturnsAllKeysInCamelCase()
    {
        SkipIfUnavailable();
        var client = Factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            RegisterPayload(email: "no-es-un-correo", password: "corta", consentTextHash: "xyz"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errors = await ErrorsAsync(response);

        errors.Should().ContainKeys("email", "password", "consentTextHash");
        errors.Keys.Should().OnlyContain(key => char.IsLower(key[0]));
    }

    [SkippableFact]
    public async Task Login_MissingPassword_ReturnsCamelCaseErrorKey()
    {
        SkipIfUnavailable();
        var client = Factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "paciente@cauce.local",
            password = string.Empty,
            clientId = "cauce-mobile"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errors = await ErrorsAsync(response);
        errors.Should().ContainKey("password");
        errors.Should().NotContainKey("Password");
    }

    private static object RegisterPayload(
        string email = "nuevo.paciente@cauce.local",
        string fullName = "Paciente De Prueba",
        string password = "Valida123",
        string consentTextHash = ValidConsentHash)
    {
        return new
        {
            email,
            fullName,
            password,
            consentDocumentVersion = CustomWebApplicationFactory.ConsentVersion,
            consentTextHash,
            invitationCode = (string?)null
        };
    }

    /// <summary>
    /// Extrae el diccionario <c>errors</c> del cuerpo, verificando de paso que la respuesta trae la
    /// extensión <c>errorCode</c> con el valor esperado para un fallo de validación.
    /// </summary>
    /// <param name="response">Respuesta HTTP a inspeccionar.</param>
    /// <returns>Las claves de error con sus mensajes.</returns>
    private static async Task<Dictionary<string, string[]>> ErrorsAsync(HttpResponseMessage response)
    {
        var payload = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(payload);

        document.RootElement.TryGetProperty("errorCode", out var errorCode).Should().BeTrue();
        errorCode.GetString().Should().Be("validation_error");

        document.RootElement.TryGetProperty("errors", out var errorsElement).Should().BeTrue();

        return errorsElement.EnumerateObject().ToDictionary(
            property => property.Name,
            property => property.Value.EnumerateArray()
                .Select(message => message.GetString() ?? string.Empty)
                .ToArray());
    }
}
