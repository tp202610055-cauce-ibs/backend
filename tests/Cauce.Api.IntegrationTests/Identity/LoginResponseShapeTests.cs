using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cauce.Api.IntegrationTests.Identity.Support;
using Cauce.Domain.Identity;
using FluentAssertions;

namespace Cauce.Api.IntegrationTests.Identity;

/// <summary>
/// Pruebas del contrato de respuesta de <c>POST /api/v1/auth/login</c>. Verifican que el cuerpo
/// incorpore la identidad del usuario, de modo que el cliente no necesite una segunda llamada ni
/// decodificar el JWT para arrancar la sesión. Requieren Docker (PostgreSQL + Redis).
/// </summary>
[Trait("Category", "Integration")]
public sealed class LoginResponseShapeTests
    : Prompt5IntegrationTestBase, IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>
{
    /// <summary>
    /// Inicializa la prueba con los fixtures compartidos.
    /// </summary>
    /// <param name="postgres">Fixture del contenedor PostgreSQL.</param>
    /// <param name="redis">Fixture del contenedor Redis/KeyDB.</param>
    public LoginResponseShapeTests(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis)
    {
    }

    [SkippableFact]
    public async Task Login_SeededPatient_ReturnsCompleteAuthenticatedUser()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        var client = Factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = patient.Email,
            password = Factory.TokenClient.ValidPassword,
            clientId = "cauce-mobile"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        root.GetProperty("accessToken").GetString().Should().NotBeNullOrEmpty();
        root.GetProperty("refreshToken").GetString().Should().NotBeNullOrEmpty();
        root.GetProperty("tokenType").GetString().Should().Be("Bearer");

        root.TryGetProperty("user", out var user).Should().BeTrue("el login debe incorporar la identidad");
        user.GetProperty("userId").GetGuid().Should().Be(patient.Id);
        user.GetProperty("keycloakId").GetString().Should().Be(patient.KeycloakId);
        user.GetProperty("email").GetString().Should().Be(patient.Email);
        user.GetProperty("role").GetString().Should().Be(UserRoles.Patient);
        user.GetProperty("fullName").GetString().Should().Be("Paciente");
        user.GetProperty("emailVerified").GetBoolean().Should().BeFalse();
        user.GetProperty("isInActivePilot").GetBoolean().Should().BeFalse();
    }

    [SkippableFact]
    public async Task Login_SeededNutritionist_ReturnsNutritionistRole()
    {
        SkipIfUnavailable();
        var nutritionist = await SeedNutritionistAsync();
        var client = Factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = nutritionist.Email,
            password = Factory.TokenClient.ValidPassword,
            clientId = "cauce-web-portal"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var user = document.RootElement.GetProperty("user");

        user.GetProperty("role").GetString().Should().Be(UserRoles.Nutritionist);
        // El nutricionista se provisiona administrativamente, ya verificado.
        user.GetProperty("emailVerified").GetBoolean().Should().BeTrue();
    }

    [SkippableFact]
    public async Task Login_KeycloakAuthenticatesButNoLocalUser_Returns500WithUserLocalMissing()
    {
        SkipIfUnavailable();
        var client = Factory.CreateClient();

        // El doble de Keycloak acepta cualquier correo con la contraseña válida, así que este caso
        // reproduce la inconsistencia: autenticación exitosa sin fila local.
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "fantasma@cauce.local",
            password = Factory.TokenClient.ValidPassword,
            clientId = "cauce-mobile"
        });

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("errorCode").GetString().Should().Be("user_local_missing");
    }
}
