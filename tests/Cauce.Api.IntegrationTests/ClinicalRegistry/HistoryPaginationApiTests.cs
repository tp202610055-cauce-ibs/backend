using System.Net;
using System.Text.Json;
using Cauce.Api.IntegrationTests.Identity.Support;
using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Api.IntegrationTests.ClinicalRegistry;

/// <summary>
/// Pruebas de integración de la paginación del historial de comidas y de síntomas. Registros con el mismo
/// <c>clientCreatedAt</c> deben aparecer una sola vez al recorrer todas las páginas.
/// </summary>
[Trait("Category", "Integration")]
public sealed class HistoryPaginationApiTests
    : IntegrationTestBase, IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>
{
    /// <summary>
    /// Inicializa la prueba con los fixtures compartidos.
    /// </summary>
    public HistoryPaginationApiTests(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis)
    {
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

    private async Task<Guid> SeedSymptomAsync(Guid patientId, DateTime clientCreatedAt)
    {
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var symptom = Symptom.Report(
            Guid.NewGuid(), Guid.NewGuid(), patientId, SymptomType.Bloating, 40, clientCreatedAt, clientCreatedAt, DateTime.UtcNow);
        db.Symptoms.Add(symptom);
        await db.SaveChangesAsync();
        return symptom.Id;
    }

    [SkippableTheory]
    [InlineData("meals", "mealId")]
    [InlineData("symptoms", "symptomId")]
    public async Task GetHistory_SameClientCreatedAt_PagesReturnEachRecordOnce(string resource, string idProperty)
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        var client = PatientClient(patient.KeycloakId, patient.Email);
        var clientCreatedAt = DateTime.UtcNow.AddHours(-1);
        var seeded = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            seeded.Add(resource == "meals"
                ? await SeedMealAsync(patient.Id, clientCreatedAt)
                : await SeedSymptomAsync(patient.Id, clientCreatedAt));
        }

        var from = Uri.EscapeDataString(clientCreatedAt.AddMinutes(-1).ToString("O"));
        var to = Uri.EscapeDataString(clientCreatedAt.AddMinutes(1).ToString("O"));
        var paged = new List<Guid>();
        for (var page = 1; page <= 3; page++)
        {
            var response = await client.GetAsync($"/api/v1/{resource}?from={from}&to={to}&page={page}&pageSize=1");
            var raw = await response.Content.ReadAsStringAsync();
            response.StatusCode.Should().Be(HttpStatusCode.OK, raw);
            var items = JsonDocument.Parse(raw).RootElement.GetProperty("items");
            items.GetArrayLength().Should().Be(1, raw);
            paged.Add(items[0].GetProperty(idProperty).GetGuid());
        }

        paged.Should().OnlyHaveUniqueItems().And.BeEquivalentTo(seeded);
    }
}
