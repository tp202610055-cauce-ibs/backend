using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cauce.Api.IntegrationTests.Identity.Support;
using FluentAssertions;

namespace Cauce.Api.IntegrationTests.Identity;

/// <summary>
/// Pruebas de la sincronización de <c>emailVerified</c> en <c>POST /api/v1/auth/login</c> (acta A39).
/// El paciente verifica su correo con el enlace que emite Keycloak, que nunca pasa por el backend, así
/// que la copia local queda desactualizada hasta el siguiente inicio de sesión. Requieren Docker
/// (PostgreSQL + Redis).
/// </summary>
[Trait("Category", "Integration")]
public sealed class LoginEmailVerificationSyncTests
    : IntegrationTestBase, IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>
{
    /// <summary>
    /// Inicializa la prueba con los fixtures compartidos.
    /// </summary>
    /// <param name="postgres">Fixture del contenedor PostgreSQL.</param>
    /// <param name="redis">Fixture del contenedor Redis/KeyDB.</param>
    public LoginEmailVerificationSyncTests(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis)
    {
    }

    [SkippableFact]
    public async Task Login_VerifiedInKeycloakOnly_SyncsAndReturnsVerified()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();

        // El paciente sembrado nace sin verificar en la copia local. Se simula que siguió el enlace de
        // Keycloak, que actualiza el realm sin avisarle al backend.
        Factory.KeycloakClient.MarkEmailVerifiedInKeycloak(patient.KeycloakId);

        var user = await LoginAndReadUserAsync(patient.Email);

        user.GetProperty("emailVerified").GetBoolean().Should().BeTrue();
    }

    [SkippableFact]
    public async Task Login_VerifiedInKeycloakOnly_PersistsSoTheNextLoginDoesNotDependOnKeycloak()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        Factory.KeycloakClient.MarkEmailVerifiedInKeycloak(patient.KeycloakId);

        await LoginAndReadUserAsync(patient.Email);

        // La Admin API deja de responder después del primer login. Si el valor se persistió, el
        // segundo login sigue devolviendo el correo como verificado.
        Factory.KeycloakClient.EmailVerifiedFailure = new HttpRequestException("Admin API caída");
        var user = await LoginAndReadUserAsync(patient.Email);

        user.GetProperty("emailVerified").GetBoolean().Should().BeTrue();
    }

    [SkippableFact]
    public async Task Login_AdminApiFails_SucceedsWithTheLocalValue()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        Factory.KeycloakClient.EmailVerifiedFailure = new HttpRequestException("Admin API caída");

        var user = await LoginAndReadUserAsync(patient.Email);

        // Una sincronización auxiliar que falla no puede tumbar un login válido (decisión D2).
        user.GetProperty("emailVerified").GetBoolean().Should().BeFalse();
    }

    [SkippableFact]
    public async Task Login_UnverifiedInKeycloakButVerifiedLocally_DoesNotRevert()
    {
        SkipIfUnavailable();

        // El nutricionista se provisiona administrativamente y nace verificado en la copia local,
        // mientras que el doble de Keycloak lo reporta como no verificado.
        var nutritionist = await SeedNutritionistAsync();

        var user = await LoginAndReadUserAsync(nutritionist.Email, "cauce-web-portal");

        user.GetProperty("emailVerified").GetBoolean().Should().BeTrue();
    }

    private async Task<JsonElement> LoginAndReadUserAsync(string email, string clientId = "cauce-mobile")
    {
        var client = Factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email,
            password = Factory.TokenClient.ValidPassword,
            clientId
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("user").Clone();
    }
}
