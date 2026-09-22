using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cauce.Api.IntegrationTests.Identity.Support;
using Cauce.Domain.ClinicalRegistry;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Api.IntegrationTests.ClinicalRegistry;

/// <summary>
/// Pruebas de integración del rechazo de una evaluación IBS-SSS periódica enviada antes de tiempo.
/// El ciclo del protocolo es de 14 días: aceptar una evaluación adelantada acortaría el intervalo y
/// dejaría dos mediciones demasiado próximas como para compararlas.
/// </summary>
[Trait("Category", "Integration")]
public sealed class IbsSssEarlySubmissionApiTests
    : IntegrationTestBase, IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>
{
    /// <summary>
    /// Inicializa la prueba con los fixtures compartidos.
    /// </summary>
    public IbsSssEarlySubmissionApiTests(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis)
    {
    }

    private static object Body(string assessmentType) => new
    {
        assessmentType,
        painSeverity = 30,
        painFrequency = 30,
        bloatingSeverity = 30,
        bowelHabitsDissatisfaction = 30,
        lifeInterference = 30
    };

    private async Task SeedScheduleAsync(Guid patientId, DateTime dueDate)
    {
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        db.IbsSssSchedules.Add(
            IbsSssAssessmentSchedule.Create(Guid.NewGuid(), patientId, dueDate, DateTime.UtcNow.AddDays(-14)));
        await db.SaveChangesAsync();
    }

    private async Task<int> AssessmentCountAsync(Guid patientId)
    {
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        return await db.IbsSssAssessments.AsNoTracking().CountAsync(a => a.PatientId == patientId);
    }

    [SkippableFact]
    public async Task Submit_PeriodicWellBeforeDueDate_Returns422WithTheDates()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        await SeedPatientProfileAsync(patient.Id);
        var dueDate = DateTime.UtcNow.AddDays(10);
        await SeedScheduleAsync(patient.Id, dueDate);

        var response = await PatientClient(patient.KeycloakId, patient.Email)
            .PostAsJsonAsync("/api/v1/ibs-sss", Body("Periodic"));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errorCode").GetString().Should().Be("ibs_sss_assessment_too_early");

        // El cliente necesita saber cuándo puede volver, no solo que llegó temprano.
        body.GetProperty("dueDate").GetDateTime().Should().BeCloseTo(dueDate, TimeSpan.FromSeconds(2));
        body.GetProperty("acceptedFrom").GetDateTime()
            .Should().BeCloseTo(dueDate - IbsSssAssessmentSchedule.EarlySubmissionTolerance, TimeSpan.FromSeconds(2));

        (await AssessmentCountAsync(patient.Id)).Should().Be(0);
    }

    [SkippableFact]
    public async Task Submit_PeriodicWithinTheTolerance_IsAccepted()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        await SeedPatientProfileAsync(patient.Id);

        // Doce horas antes del vencimiento: dentro de la tolerancia de 24 h, que existe porque el
        // ciclo se ancla a medianoche UTC y el paciente responde en hora de Lima.
        await SeedScheduleAsync(patient.Id, DateTime.UtcNow.AddHours(12));

        var response = await PatientClient(patient.KeycloakId, patient.Email)
            .PostAsJsonAsync("/api/v1/ibs-sss", Body("Periodic"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await AssessmentCountAsync(patient.Id)).Should().Be(1);
    }

    [SkippableFact]
    public async Task Submit_PeriodicAfterDueDate_IsAccepted()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        await SeedPatientProfileAsync(patient.Id);
        await SeedScheduleAsync(patient.Id, DateTime.UtcNow.AddDays(-2));

        var response = await PatientClient(patient.KeycloakId, patient.Email)
            .PostAsJsonAsync("/api/v1/ibs-sss", Body("Periodic"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [SkippableFact]
    public async Task Submit_PeriodicWithoutAnOpenSchedule_IsAccepted()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        await SeedPatientProfileAsync(patient.Id);

        // Sin agenda abierta no hay fecha esperada contra la cual estar adelantado.
        var response = await PatientClient(patient.KeycloakId, patient.Email)
            .PostAsJsonAsync("/api/v1/ibs-sss", Body("Periodic"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [SkippableFact]
    public async Task Submit_BaselineIsNeverTooEarly()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        await SeedPatientProfileAsync(patient.Id);
        await SeedScheduleAsync(patient.Id, DateTime.UtcNow.AddDays(10));

        // La línea base no pertenece al ciclo periódico: la agenda no la gobierna.
        var response = await PatientClient(patient.KeycloakId, patient.Email)
            .PostAsJsonAsync("/api/v1/ibs-sss", Body("Baseline"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
