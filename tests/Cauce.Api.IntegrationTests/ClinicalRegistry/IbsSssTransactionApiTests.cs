using System.Net;
using System.Net.Http.Json;
using Cauce.Api.IntegrationTests.Identity.Support;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Api.IntegrationTests.ClinicalRegistry;

/// <summary>
/// Pruebas de integración de la atomicidad del registro de una evaluación IBS-SSS.
///
/// <para>La línea base cierra además el onboarding del paciente, y ese cierre es un segundo
/// <c>SaveChanges</c> en otro handler. Sin una transacción que abarque los dos, un fallo en el cierre
/// dejaba la evaluación ya confirmada y el perfil sin cerrar: un estado que ningún flujo posterior
/// repara, porque la línea base es única y no puede repetirse.</para>
/// </summary>
[Trait("Category", "Integration")]
public sealed class IbsSssTransactionApiTests
    : IntegrationTestBase, IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>
{
    /// <summary>
    /// Inicializa la prueba con los fixtures compartidos.
    /// </summary>
    public IbsSssTransactionApiTests(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis)
    {
    }

    private static object BaselineBody() => new
    {
        assessmentType = "Baseline",
        painSeverity = 50,
        painFrequency = 50,
        bloatingSeverity = 50,
        bowelHabitsDissatisfaction = 50,
        lifeInterference = 50
    };

    [SkippableFact]
    public async Task SubmitBaseline_WhenOnboardingCompletionFails_PersistsNothing()
    {
        SkipIfUnavailable();

        // Paciente sin perfil clínico: el cierre del onboarding falla después de que la evaluación y
        // su agenda ya se guardaron. Es el escenario exacto que la transacción tiene que revertir.
        var patient = await SeedPatientAsync();

        var response = await PatientClient(patient.KeycloakId, patient.Email)
            .PostAsJsonAsync("/api/v1/ibs-sss", BaselineBody());

        response.IsSuccessStatusCode.Should().BeFalse();

        var (scope, db) = CreateDbScope();
        using var _ = scope;
        (await db.IbsSssAssessments.AsNoTracking().CountAsync(a => a.PatientId == patient.Id))
            .Should().Be(0, "la evaluación tiene que revertirse junto con el cierre del onboarding");
        (await db.IbsSssSchedules.AsNoTracking().CountAsync(s => s.PatientId == patient.Id))
            .Should().Be(0, "la agenda de la próxima evaluación se crea en la misma transacción");
        (await db.OutboxMessages.AsNoTracking().CountAsync())
            .Should().Be(0, "el evento de outbox no debe publicarse si la evaluación no existe");
    }

    [SkippableFact]
    public async Task SubmitBaseline_HappyPath_PersistsAssessmentScheduleAndClosesOnboarding()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        await SeedPatientProfileAsync(patient.Id);

        var response = await PatientClient(patient.KeycloakId, patient.Email)
            .PostAsJsonAsync("/api/v1/ibs-sss", BaselineBody());

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var (scope, db) = CreateDbScope();
        using var _ = scope;
        (await db.IbsSssAssessments.AsNoTracking().CountAsync(a => a.PatientId == patient.Id)).Should().Be(1);
        (await db.IbsSssSchedules.AsNoTracking().CountAsync(s => s.PatientId == patient.Id)).Should().Be(1);
        (await db.PatientProfiles.AsNoTracking().SingleAsync(p => p.UserId == patient.Id))
            .OnboardingCompleted.Should().BeTrue();
    }
}
