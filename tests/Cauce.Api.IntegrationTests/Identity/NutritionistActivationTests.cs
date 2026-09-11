using System.Net;
using System.Net.Http.Json;
using Cauce.Api.IntegrationTests.Identity.Support;
using Cauce.Domain.Auditing.Enums;
using Cauce.Domain.Identity;
using Cauce.Domain.Identity.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Api.IntegrationTests.Identity;

/// <summary>
/// Pruebas de la activación de nutricionistas (acta A51) por sus dos puntos de entrada: el login, donde la
/// identidad aparece recién dentro del handler, y el behavior del pipeline, que cubre cualquier otra
/// petición autenticada, incluidas las consultas que no confirman cambios propios. Requieren Docker
/// (PostgreSQL + Redis).
/// </summary>
[Trait("Category", "Integration")]
public sealed class NutritionistActivationTests
    : Prompt5IntegrationTestBase, IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>
{
    private const string AssignedPatientsEndpoint = "/api/v1/nutritionists/me/patients";

    /// <summary>
    /// Inicializa la prueba con los fixtures compartidos.
    /// </summary>
    /// <param name="postgres">Fixture del contenedor PostgreSQL.</param>
    /// <param name="redis">Fixture del contenedor Redis/KeyDB.</param>
    public NutritionistActivationTests(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis)
    {
    }

    [SkippableFact]
    public async Task Login_PendingNutritionist_ActivatesAndAuditsTheLoginTrigger()
    {
        SkipIfUnavailable();
        var nutritionist = await SeedNutritionistAsync(active: false);

        var response = await Factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = nutritionist.Email,
            password = Factory.TokenClient.ValidPassword,
            clientId = "cauce-mobile"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await StatusOfAsync(nutritionist.Id)).Should().Be(UserStatus.Active);

        var log = (await ActivationLogsAsync(nutritionist.Id)).Should().ContainSingle().Subject;
        log.ActorUserId.Should().Be(nutritionist.Id);
        TriggerOf(log).Should().Be("login");
    }

    [SkippableFact]
    public async Task AuthenticatedQuery_PendingNutritionist_ActivatesEvenThoughTheHandlerDoesNotPersist()
    {
        SkipIfUnavailable();
        var nutritionist = await SeedNutritionistAsync(active: false);

        // Es una consulta: su handler nunca llama a SaveChanges. Si la activación dependiera de él, se
        // perdería. El token simula el que el portal obtiene directo de Keycloak, sin pasar por el login.
        var response = await NutritionistClient(nutritionist.KeycloakId, nutritionist.Email)
            .GetAsync(AssignedPatientsEndpoint);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await StatusOfAsync(nutritionist.Id)).Should().Be(UserStatus.Active);

        var log = (await ActivationLogsAsync(nutritionist.Id)).Should().ContainSingle().Subject;
        log.ActorUserId.Should().Be(nutritionist.Id);
        TriggerOf(log).Should().Be("authenticated_request");
    }

    [SkippableFact]
    public async Task AuthenticatedQuery_RepeatedRequests_ActivatesOnlyOnce()
    {
        SkipIfUnavailable();
        var nutritionist = await SeedNutritionistAsync(active: false);
        var client = NutritionistClient(nutritionist.KeycloakId, nutritionist.Email);

        (await client.GetAsync(AssignedPatientsEndpoint)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync(AssignedPatientsEndpoint)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await ActivationLogsAsync(nutritionist.Id)).Should().ContainSingle();
    }

    [SkippableFact]
    public async Task AuthenticatedQuery_ActiveNutritionist_DoesNotAudit()
    {
        SkipIfUnavailable();
        var nutritionist = await SeedNutritionistAsync();

        var response = await NutritionistClient(nutritionist.KeycloakId, nutritionist.Email)
            .GetAsync(AssignedPatientsEndpoint);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ActivationLogsAsync(nutritionist.Id)).Should().BeEmpty();
    }

    [SkippableFact]
    public async Task AuthenticatedRequest_PendingPatient_StaysPending()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();

        // Sin perfil clínico la respuesta es 404; lo que importa es que el paciente no se active: su
        // activación depende de verificar el correo, no de autenticarse.
        await PatientClient(patient.KeycloakId, patient.Email).GetAsync("/api/v1/patients/profile");

        (await StatusOfAsync(patient.Id)).Should().Be(UserStatus.PendingActivation);
        (await ActivationLogsAsync(patient.Id)).Should().BeEmpty();
    }

    private async Task<UserStatus> StatusOfAsync(Guid userId)
    {
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        return await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.Status).SingleAsync();
    }

    private Task<IReadOnlyList<Cauce.Domain.Auditing.AuditLog>> ActivationLogsAsync(Guid userId) =>
        AuditLogsAsync(nameof(User), userId, AuditActionType.AccountActivation);

    private static string? TriggerOf(Cauce.Domain.Auditing.AuditLog log)
    {
        // additional_context es jsonb: Postgres normaliza el espaciado, así que se compara el valor leído
        // del JSON y no el texto.
        using var context = System.Text.Json.JsonDocument.Parse(log.AdditionalContext!);
        return context.RootElement.GetProperty("trigger").GetString();
    }
}
