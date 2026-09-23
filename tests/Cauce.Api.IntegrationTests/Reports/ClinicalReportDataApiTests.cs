using Cauce.Api.IntegrationTests.Identity.Support;
using Cauce.Application.Common.Interfaces.Reports;
using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry.Enums;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Cauce.Api.IntegrationTests.Reports;

/// <summary>
/// Pruebas de integración de los datos que alimentan el reporte clínico (HU0024 CA01). Se ejercitan
/// contra PostgreSQL real a través de <see cref="IClinicalReportDataReader"/>, que es donde viven las
/// consultas nuevas; el PDF en sí se comprueba aparte, porque su texto queda dentro de fuentes
/// embebidas y no puede leerse de vuelta de forma confiable.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ClinicalReportDataApiTests
    : IntegrationTestBase, IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>
{
    /// <summary>
    /// Inicializa la prueba con los fixtures compartidos.
    /// </summary>
    public ClinicalReportDataApiTests(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis)
    {
    }

    private async Task<string> SeedMealAsync(Guid patientId, DateTime consumedAt)
    {
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var food = await db.FoodItems.AsNoTracking().FirstAsync();
        var meal = Meal.Register(
            Guid.NewGuid(), Guid.NewGuid(), patientId, MealTime.Lunch, consumedAt, consumedAt,
            new[] { new MealItemInput(food.Id, null, 120m, MeasurementUnit.Grams) }, DateTime.UtcNow);
        db.Meals.Add(meal);
        await db.SaveChangesAsync();
        return food.Name;
    }

    private async Task SeedSymptomAsync(Guid patientId, SymptomType type, int intensity, DateTime occurredAt)
    {
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        db.Symptoms.Add(Symptom.Report(
            Guid.NewGuid(), Guid.NewGuid(), patientId, type, intensity, occurredAt, occurredAt, DateTime.UtcNow));
        await db.SaveChangesAsync();
    }

    private async Task<Application.Reports.Contracts.ClinicalReportData> ReadAsync(Guid patientId)
    {
        var (scope, _) = CreateDbScope();
        using var _scope = scope;
        var reader = scope.ServiceProvider.GetRequiredService<IClinicalReportDataReader>();
        var end = DateOnly.FromDateTime(DateTime.UtcNow);
        return await reader.GetReportDataAsync(patientId, end.AddDays(-90), end, nutritionistId: null);
    }

    [SkippableFact]
    public async Task ReportData_CarriesTheMealHistoryAndNotOnlyTheCount()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        await SeedPatientProfileAsync(patient.Id);
        var foodName = await SeedMealAsync(patient.Id, DateTime.UtcNow.AddDays(-3));
        await SeedMealAsync(patient.Id, DateTime.UtcNow.AddDays(-1));

        var data = await ReadAsync(patient.Id);

        data.MealCount.Should().Be(2);
        data.Meals.Should().HaveCount(2);
        data.Meals.Should().BeInAscendingOrder(meal => meal.ConsumedAt);
        data.Meals[0].MealTime.Should().Be(nameof(MealTime.Lunch));
        data.Meals[0].Items.Should().Contain(foodName);
    }

    [SkippableFact]
    public async Task ReportData_SummarisesSymptomIntensityAndNotOnlyOccurrences()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        await SeedPatientProfileAsync(patient.Id);
        await SeedSymptomAsync(patient.Id, SymptomType.AbdominalPain, 20, DateTime.UtcNow.AddDays(-5));
        await SeedSymptomAsync(patient.Id, SymptomType.AbdominalPain, 80, DateTime.UtcNow.AddDays(-4));
        await SeedSymptomAsync(patient.Id, SymptomType.Bloating, 50, DateTime.UtcNow.AddDays(-3));

        var data = await ReadAsync(patient.Id);

        var pain = data.Symptoms.Single(s => s.SymptomType == nameof(SymptomType.AbdominalPain));
        pain.Count.Should().Be(2);
        pain.MinIntensity.Should().Be(20);
        pain.MaxIntensity.Should().Be(80);
        pain.AverageIntensity.Should().Be(50m);

        // Diez molestias leves y diez episodios severos no son lo mismo: sin la intensidad, el conteo
        // por sí solo no lo distingue.
        var bloating = data.Symptoms.Single(s => s.SymptomType == nameof(SymptomType.Bloating));
        bloating.AverageIntensity.Should().Be(50m);
    }

    [SkippableFact]
    public async Task ReportData_ListsEachSymptomChronologicallyWithItsIntensity()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        await SeedPatientProfileAsync(patient.Id);
        await SeedSymptomAsync(patient.Id, SymptomType.Diarrhea, 70, DateTime.UtcNow.AddDays(-2));
        await SeedSymptomAsync(patient.Id, SymptomType.Flatulence, 30, DateTime.UtcNow.AddDays(-6));

        var data = await ReadAsync(patient.Id);

        data.SymptomEntries.Should().HaveCount(2);
        data.SymptomEntries.Should().BeInAscendingOrder(entry => entry.OccurredAt);
        data.SymptomEntries[0].SymptomType.Should().Be(nameof(SymptomType.Flatulence));
        data.SymptomEntries[0].Intensity.Should().Be(30);
        data.SymptomEntries[1].Intensity.Should().Be(70);
    }

    [SkippableFact]
    public async Task ReportData_IdentifiesThePatientByCode()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        await SeedPatientProfileAsync(patient.Id);
        await SeedMealAsync(patient.Id, DateTime.UtcNow.AddDays(-1));

        var data = await ReadAsync(patient.Id);

        data.PatientCode.Should().StartWith("PAC-");
        data.PatientInitials.Should().NotBeNullOrWhiteSpace();
    }

    [SkippableFact]
    public async Task ReportData_OutsideThePeriod_IsExcluded()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        await SeedPatientProfileAsync(patient.Id);
        await SeedMealAsync(patient.Id, DateTime.UtcNow.AddDays(-200));
        await SeedSymptomAsync(patient.Id, SymptomType.Nausea, 40, DateTime.UtcNow.AddDays(-200));

        var data = await ReadAsync(patient.Id);

        data.Meals.Should().BeEmpty();
        data.SymptomEntries.Should().BeEmpty();
        data.Symptoms.Should().BeEmpty();
    }
}
