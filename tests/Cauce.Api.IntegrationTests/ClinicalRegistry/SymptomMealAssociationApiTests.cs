using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cauce.Api.IntegrationTests.Identity.Support;
using Cauce.Domain.Auditing.Enums;
using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Api.IntegrationTests.ClinicalRegistry;

/// <summary>
/// Pruebas de integración de <c>PUT /symptoms/{id}/meal-association</c>, la corrección manual de la comida
/// asociada a un síntoma por el nutricionista asignado. Van por HTTP para atravesar la política de
/// autorización, el pipeline de idempotencia, el mapeo de excepciones y el trigger de auditoría reales.
/// </summary>
[Trait("Category", "Integration")]
public sealed class SymptomMealAssociationApiTests
    : IntegrationTestBase, IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>
{
    /// <summary>
    /// Inicializa la prueba con los fixtures compartidos.
    /// </summary>
    public SymptomMealAssociationApiTests(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis)
    {
    }

    private static string Url(Guid symptomId) => $"/api/v1/symptoms/{symptomId}/meal-association";

    private static async Task<HttpResponseMessage> PutWithKey(HttpClient client, string url, object content, Guid? key = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, url);
        request.Headers.Add("Idempotency-Key", (key ?? Guid.NewGuid()).ToString());
        request.Content = JsonContent.Create(content);
        return await client.SendAsync(request);
    }

    private async Task<(Guid PatientId, Guid NutritionistId, HttpClient Client)> SeedAssignedPairAsync()
    {
        var patient = await SeedPatientAsync();
        var nutritionist = await SeedNutritionistAsync();
        await AssignAsync(nutritionist.Id, patient.Id);
        return (patient.Id, nutritionist.Id, NutritionistClient(nutritionist.KeycloakId, nutritionist.Email));
    }

    private async Task<Guid> SeedMealAsync(Guid patientId, DateTime clientCreatedAt)
    {
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var foodId = await db.FoodItems.AsNoTracking().Select(f => f.Id).FirstAsync();
        var meal = Meal.Register(
            Guid.NewGuid(), Guid.NewGuid(), patientId, MealTime.Lunch, clientCreatedAt, clientCreatedAt,
            new[] { new MealItemInput(foodId, null, 100m, MeasurementUnit.Grams) }, DateTime.UtcNow);
        db.Meals.Add(meal);
        await db.SaveChangesAsync();
        return meal.Id;
    }

    private async Task<Guid> SeedSymptomAsync(Guid patientId, Guid? associatedMealId = null)
    {
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var now = DateTime.UtcNow;
        var symptom = Symptom.Report(Guid.NewGuid(), Guid.NewGuid(), patientId, SymptomType.Bloating, 40, now, now, now);
        if (associatedMealId is { } mealId)
        {
            symptom.AssociateWithMeal(mealId, now);
        }

        db.Symptoms.Add(symptom);
        await db.SaveChangesAsync();
        return symptom.Id;
    }

    private async Task<Symptom> ReadSymptomAsync(Guid symptomId)
    {
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        return await db.Symptoms.AsNoTracking().SingleAsync(symptom => symptom.Id == symptomId);
    }

    private static async Task<string?> ErrorCodeAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("errorCode").GetString();
    }

    [SkippableFact]
    public async Task SetMealAssociation_MealOutsideTheWindow_Returns204AndAssociatesIt()
    {
        SkipIfUnavailable();
        var (patientId, _, client) = await SeedAssignedPairAsync();
        var mealId = await SeedMealAsync(patientId, DateTime.UtcNow.AddDays(-3));
        var symptomId = await SeedSymptomAsync(patientId);

        var response = await PutWithKey(client, Url(symptomId), new { mealId });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var stored = await ReadSymptomAsync(symptomId);
        stored.AssociatedMealId.Should().Be(mealId);
        stored.HasMealAssociation.Should().BeTrue();
    }

    [SkippableFact]
    public async Task SetMealAssociation_NullMeal_Returns204AndUnlinksTheSymptom()
    {
        SkipIfUnavailable();
        var (patientId, _, client) = await SeedAssignedPairAsync();
        var mealId = await SeedMealAsync(patientId, DateTime.UtcNow.AddHours(-1));
        var symptomId = await SeedSymptomAsync(patientId, mealId);

        var response = await PutWithKey(client, Url(symptomId), new { mealId = (Guid?)null });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var stored = await ReadSymptomAsync(symptomId);
        stored.AssociatedMealId.Should().BeNull();
        stored.HasMealAssociation.Should().BeFalse();
    }

    [SkippableFact]
    public async Task SetMealAssociation_WritesTheExplicitRowAndTheTriggerRowWithTheNutritionistAsActor()
    {
        SkipIfUnavailable();
        var (patientId, nutritionistId, client) = await SeedAssignedPairAsync();
        var previousMealId = await SeedMealAsync(patientId, DateTime.UtcNow.AddHours(-1));
        var newMealId = await SeedMealAsync(patientId, DateTime.UtcNow.AddDays(-2));
        var symptomId = await SeedSymptomAsync(patientId, previousMealId);

        await PutWithKey(client, Url(symptomId), new { mealId = newMealId });

        // Excepción a la no duplicación de DEC-B5-01, como las de recommendations: la fila explícita guarda
        // qué comida había y cuál quedó; la del trigger, solo hashes.
        var explicitRow = (await AuditLogsAsync(nameof(Symptom), symptomId, AuditActionType.MealAssociationCorrection))
            .Should().ContainSingle().Subject;
        explicitRow.ActorUserId.Should().Be(nutritionistId);
        using var context = JsonDocument.Parse(explicitRow.AdditionalContext!);
        context.RootElement.GetProperty("previous_meal_id").GetGuid().Should().Be(previousMealId);
        context.RootElement.GetProperty("new_meal_id").GetGuid().Should().Be(newMealId);

        var triggerRows = await AuditLogsAsync(nameof(Symptom), symptomId, AuditActionType.Update);
        triggerRows.Should().ContainSingle().Which.ActorUserId.Should().Be(nutritionistId);
    }

    [SkippableFact]
    public async Task SetMealAssociation_SameMealAsBefore_WritesTheExplicitRowButNoTriggerRow()
    {
        SkipIfUnavailable();
        var (patientId, _, client) = await SeedAssignedPairAsync();
        var mealId = await SeedMealAsync(patientId, DateTime.UtcNow.AddHours(-1));
        var symptomId = await SeedSymptomAsync(patientId, mealId);

        var response = await PutWithKey(client, Url(symptomId), new { mealId });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var explicitRow = (await AuditLogsAsync(nameof(Symptom), symptomId, AuditActionType.MealAssociationCorrection))
            .Should().ContainSingle().Subject;
        using var context = JsonDocument.Parse(explicitRow.AdditionalContext!);
        context.RootElement.GetProperty("previous_meal_id").GetGuid().Should().Be(mealId);
        context.RootElement.GetProperty("new_meal_id").GetGuid().Should().Be(mealId);

        // El trigger de symptoms no tiene cláusula WHEN: dispararía ante cualquier UPDATE, aunque no cambie
        // ningún valor. No hay fila porque EF Core no emite el UPDATE cuando ninguna propiedad cambió.
        (await AuditLogsAsync(nameof(Symptom), symptomId, AuditActionType.Update)).Should().BeEmpty();
    }

    [SkippableFact]
    public async Task SetMealAssociation_NutritionistNotAssigned_Returns403AndLeavesTheSymptomUntouched()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        var stranger = await SeedNutritionistAsync();
        var mealId = await SeedMealAsync(patient.Id, DateTime.UtcNow.AddHours(-1));
        var symptomId = await SeedSymptomAsync(patient.Id, mealId);

        var response = await PutWithKey(
            NutritionistClient(stranger.KeycloakId, stranger.Email), Url(symptomId), new { mealId = (Guid?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ErrorCodeAsync(response)).Should().Be("unauthorized_patient_access");
        (await ReadSymptomAsync(symptomId)).AssociatedMealId.Should().Be(mealId);
    }

    [SkippableFact]
    public async Task SetMealAssociation_MealOfAnotherPatient_Returns403AndLeavesTheSymptomUntouched()
    {
        SkipIfUnavailable();
        var (patientId, _, client) = await SeedAssignedPairAsync();
        var otherPatient = await SeedPatientAsync();
        var foreignMealId = await SeedMealAsync(otherPatient.Id, DateTime.UtcNow.AddHours(-1));
        var symptomId = await SeedSymptomAsync(patientId);

        var response = await PutWithKey(client, Url(symptomId), new { mealId = foreignMealId });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ErrorCodeAsync(response)).Should().Be("patient_resource_access_denied");
        (await ReadSymptomAsync(symptomId)).HasMealAssociation.Should().BeFalse();
    }

    [SkippableFact]
    public async Task SetMealAssociation_UnknownSymptom_Returns404()
    {
        SkipIfUnavailable();
        var (_, _, client) = await SeedAssignedPairAsync();

        var response = await PutWithKey(client, Url(Guid.NewGuid()), new { mealId = (Guid?)null });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ErrorCodeAsync(response)).Should().Be("symptom_not_found");
    }

    [SkippableFact]
    public async Task SetMealAssociation_AsThePatient_Returns403()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        var symptomId = await SeedSymptomAsync(patient.Id);

        var response = await PutWithKey(
            PatientClient(patient.KeycloakId, patient.Email), Url(symptomId), new { mealId = (Guid?)null });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [SkippableFact]
    public async Task SetMealAssociation_WithoutIdempotencyKey_Returns400()
    {
        SkipIfUnavailable();
        var (patientId, _, client) = await SeedAssignedPairAsync();
        var symptomId = await SeedSymptomAsync(patientId);

        var response = await client.PutAsJsonAsync(Url(symptomId), new { mealId = (Guid?)null });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("errors").GetProperty("clientGuid").EnumerateArray().Should().NotBeEmpty();
    }

    [SkippableFact]
    public async Task SetMealAssociation_SameKeyTwice_ReplaysWithoutASecondAuditRow()
    {
        SkipIfUnavailable();
        var (patientId, _, client) = await SeedAssignedPairAsync();
        var mealId = await SeedMealAsync(patientId, DateTime.UtcNow.AddHours(-1));
        var symptomId = await SeedSymptomAsync(patientId);
        var key = Guid.NewGuid();

        var first = await PutWithKey(client, Url(symptomId), new { mealId }, key);
        var second = await PutWithKey(client, Url(symptomId), new { mealId }, key);

        first.StatusCode.Should().Be(HttpStatusCode.NoContent);
        second.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await AuditLogsAsync(nameof(Symptom), symptomId, AuditActionType.MealAssociationCorrection))
            .Should().ContainSingle("un reintento idempotente es una repetición, no una segunda corrección");
    }

    [SkippableFact]
    public async Task SetMealAssociation_SameKeyWithAnotherMeal_Returns409AndKeepsTheFirstCorrection()
    {
        SkipIfUnavailable();
        var (patientId, _, client) = await SeedAssignedPairAsync();
        var firstMealId = await SeedMealAsync(patientId, DateTime.UtcNow.AddHours(-1));
        var secondMealId = await SeedMealAsync(patientId, DateTime.UtcNow.AddHours(-2));
        var symptomId = await SeedSymptomAsync(patientId);
        var key = Guid.NewGuid();

        await PutWithKey(client, Url(symptomId), new { mealId = firstMealId }, key);
        var conflict = await PutWithKey(client, Url(symptomId), new { mealId = secondMealId }, key);

        conflict.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ErrorCodeAsync(conflict)).Should().Be("idempotency_mismatch");
        (await ReadSymptomAsync(symptomId)).AssociatedMealId.Should().Be(firstMealId);
    }
}
