using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cauce.Api.IntegrationTests.Identity.Support;
using Cauce.Application.Common.Exceptions;
using Cauce.Domain.Auditing.Enums;
using Cauce.Domain.Identity;
using Cauce.Domain.Identity.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Api.IntegrationTests.Identity;

/// <summary>
/// Pruebas de los endpoints administrativos de nutricionistas (acta A52): la provisión crea la cuenta
/// pendiente y pide a Keycloak el enlace para definir la contraseña, y el reenvío lo vuelve a pedir solo
/// para cuentas pendientes. Requieren Docker (PostgreSQL + Redis).
/// </summary>
[Trait("Category", "Integration")]
public sealed class AdminNutritionistsApiTests
    : IntegrationTestBase, IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>
{
    private const string ProvisionEndpoint = "/api/v1/admin/nutritionists";

    /// <summary>
    /// Inicializa la prueba con los fixtures compartidos.
    /// </summary>
    /// <param name="postgres">Fixture del contenedor PostgreSQL.</param>
    /// <param name="redis">Fixture del contenedor Redis/KeyDB.</param>
    public AdminNutritionistsApiTests(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis)
    {
    }

    [SkippableFact]
    public async Task Provision_ValidRequest_CreatesAPendingNutritionistAndRequestsTheLink()
    {
        SkipIfUnavailable();

        var (status, body) = await ProvisionAsync();

        status.Should().Be(HttpStatusCode.Created);
        body.GetProperty("activationEmailSent").GetBoolean().Should().BeTrue();

        var user = await UserAsync(body.GetProperty("userId").GetGuid());
        user.Status.Should().Be(UserStatus.PendingActivation);
        Factory.KeycloakClient.UpdatePasswordEmailsSent.Should().ContainSingle().Which.Should().Be(user.KeycloakId);
        // Ninguna contraseña viaja por el correo del backend.
        Factory.EmailSender.SentEmails.Should().BeEmpty();
    }

    [SkippableFact]
    public async Task Provision_LinkRequestFails_Returns201WithTheFlagFalseAndKeepsTheAccount()
    {
        SkipIfUnavailable();
        Factory.KeycloakClient.UpdatePasswordEmailFailure = new KeycloakIntegrationException("SMTP del realm caído.");

        var (status, body) = await ProvisionAsync();

        status.Should().Be(HttpStatusCode.Created);
        body.GetProperty("activationEmailSent").GetBoolean().Should().BeFalse();

        var user = await UserAsync(body.GetProperty("userId").GetGuid());
        user.Status.Should().Be(UserStatus.PendingActivation);
        // Sin compensación: borrar el usuario en Keycloak dejaría la fila local huérfana.
        Factory.KeycloakClient.DeletedUsers.Should().BeEmpty();
    }

    [SkippableFact]
    public async Task ResendActivationEmail_PendingNutritionist_Returns204SendsAgainAndAudits()
    {
        SkipIfUnavailable();
        var (_, body) = await ProvisionAsync();
        var userId = body.GetProperty("userId").GetGuid();

        var response = await AdminClient().PostAsync(ResendEndpoint(userId), content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        Factory.KeycloakClient.UpdatePasswordEmailsSent.Should().HaveCount(2);
        (await AuditLogsAsync(nameof(User), userId, AuditActionType.ActivationEmailResend)).Should().ContainSingle();
    }

    [SkippableFact]
    public async Task ResendActivationEmail_ActiveNutritionist_Returns409()
    {
        SkipIfUnavailable();
        var nutritionist = await SeedNutritionistAsync();

        var response = await AdminClient().PostAsync(ResendEndpoint(nutritionist.Id), content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ErrorCodeAsync(response)).Should().Be("nutritionist_not_pending_activation");
        Factory.KeycloakClient.UpdatePasswordEmailsSent.Should().BeEmpty();
    }

    [SkippableFact]
    public async Task ResendActivationEmail_UnknownIdentifier_Returns404()
    {
        SkipIfUnavailable();

        var response = await AdminClient().PostAsync(ResendEndpoint(Guid.NewGuid()), content: null);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ErrorCodeAsync(response)).Should().Be("nutritionist_not_found");
    }

    [SkippableFact]
    public async Task ResendActivationEmail_PatientIdentifier_Returns404()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();

        var response = await AdminClient().PostAsync(ResendEndpoint(patient.Id), content: null);

        // Una cuenta de otro rol responde igual que una inexistente.
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ErrorCodeAsync(response)).Should().Be("nutritionist_not_found");
    }

    [SkippableFact]
    public async Task ResendActivationEmail_KeycloakFails_Returns502WithoutAuditing()
    {
        SkipIfUnavailable();
        var (_, body) = await ProvisionAsync();
        var userId = body.GetProperty("userId").GetGuid();
        Factory.KeycloakClient.UpdatePasswordEmailFailure = new KeycloakIntegrationException("SMTP del realm caído.");

        var response = await AdminClient().PostAsync(ResendEndpoint(userId), content: null);

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        (await ErrorCodeAsync(response)).Should().Be("keycloak_integration_error");
        (await AuditLogsAsync(nameof(User), userId, AuditActionType.ActivationEmailResend)).Should().BeEmpty();
    }

    [SkippableFact]
    public async Task ResendActivationEmail_WithoutApiKey_Returns401()
    {
        SkipIfUnavailable();

        var response = await Factory.CreateClient().PostAsync(ResendEndpoint(Guid.NewGuid()), content: null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [SkippableFact]
    public async Task Provision_ValidRequest_ReturnsTheStatusAndTheLocationOfTheNewAccount()
    {
        SkipIfUnavailable();

        var response = await AdminClient().PostAsJsonAsync(ProvisionEndpoint, NewNutritionistBody());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var userId = document.RootElement.GetProperty("userId").GetGuid();
        document.RootElement.GetProperty("status").GetString().Should().Be("PendingActivation");
        response.Headers.Location!.AbsolutePath.Should().Be($"{ProvisionEndpoint}/{userId}");
    }

    [SkippableFact]
    public async Task GetNutritionist_ProvisionedAccount_ReturnsTheSummaryAtTheLocation()
    {
        SkipIfUnavailable();
        var client = AdminClient();
        var created = await client.PostAsJsonAsync(ProvisionEndpoint, NewNutritionistBody("Nutri Consultada"));

        var response = await client.GetAsync(created.Headers.Location);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;
        root.GetProperty("fullName").GetString().Should().Be("Nutri Consultada");
        root.GetProperty("email").GetString().Should().EndWith("@cauce.local");
        root.GetProperty("status").GetString().Should().Be("PendingActivation");
        created.Headers.Location!.AbsolutePath.Should().EndWith(root.GetProperty("userId").GetGuid().ToString());
    }

    [SkippableFact]
    public async Task GetNutritionist_UnknownIdentifier_Returns404()
    {
        SkipIfUnavailable();

        var response = await AdminClient().GetAsync($"{ProvisionEndpoint}/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ErrorCodeAsync(response)).Should().Be("nutritionist_not_found");
    }

    [SkippableFact]
    public async Task GetNutritionist_PatientIdentifier_Returns404()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();

        var response = await AdminClient().GetAsync($"{ProvisionEndpoint}/{patient.Id}");

        // Una cuenta de otro rol responde igual que una inexistente.
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ErrorCodeAsync(response)).Should().Be("nutritionist_not_found");
    }

    [SkippableFact]
    public async Task GetNutritionist_WithoutApiKey_Returns401()
    {
        SkipIfUnavailable();
        var nutritionist = await SeedNutritionistAsync();

        var response = await Factory.CreateClient().GetAsync($"{ProvisionEndpoint}/{nutritionist.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static object NewNutritionistBody(string fullName = "Nutri Admin") => new
    {
        email = $"nutri-{Guid.NewGuid():N}@cauce.local",
        fullName
    };

    private static string ResendEndpoint(Guid nutritionistId) =>
        $"{ProvisionEndpoint}/{nutritionistId}/activation-email";

    private HttpClient AdminClient()
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Admin-Api-Key", CustomWebApplicationFactory.AdminApiKey);
        return client;
    }

    private async Task<(HttpStatusCode Status, JsonElement Body)> ProvisionAsync()
    {
        var response = await AdminClient().PostAsJsonAsync(ProvisionEndpoint, new
        {
            email = $"nutri-{Guid.NewGuid():N}@cauce.local",
            fullName = "Nutri Admin"
        });

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return (response.StatusCode, document.RootElement.Clone());
    }

    private async Task<User> UserAsync(Guid userId)
    {
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        return await db.Users.AsNoTracking().SingleAsync(u => u.Id == userId);
    }

    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("errorCode").GetString();
    }
}
