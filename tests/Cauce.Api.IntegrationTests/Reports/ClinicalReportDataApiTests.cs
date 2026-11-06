using Cauce.Api.IntegrationTests.Identity.Support;
using Cauce.Application.Common.Interfaces.Reports;
using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry.Enums;
using Cauce.Domain.Recommendations;
using Cauce.Domain.Recommendations.Enums;
using Cauce.Domain.Recommendations.ValueObjects;
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
    private async Task<Application.Reports.Contracts.ClinicalReportData> ReadPeriodAsync(Guid patientId, DateOnly start, DateOnly end)
    {
        var (scope, _) = CreateDbScope();
        using var _scope = scope;
        var reader = scope.ServiceProvider.GetRequiredService<IClinicalReportDataReader>();
        return await reader.GetReportDataAsync(patientId, start, end, nutritionistId: null);
    }

    [SkippableFact]
    public async Task ReportData_CountsDaysWithRecordsByLimaDayOverThePeriodDays()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        await SeedPatientProfileAsync(patient.Id);
        // 01/08 13:00 Lima y 02/08 22:00 Lima (03/08 03:00 UTC) son dos días de Lima distintos.
        await SeedMealAsync(patient.Id, new DateTime(2026, 8, 1, 18, 0, 0, DateTimeKind.Utc));
        await SeedSymptomAsync(patient.Id, SymptomType.Bloating, 40, new DateTime(2026, 8, 1, 20, 0, 0, DateTimeKind.Utc));
        await SeedMealAsync(patient.Id, new DateTime(2026, 8, 3, 3, 0, 0, DateTimeKind.Utc));
        // 01/09 02:00 UTC todavía es 31/08 en Lima: entra al período de agosto.
        await SeedSymptomAsync(patient.Id, SymptomType.Nausea, 30, new DateTime(2026, 9, 1, 2, 0, 0, DateTimeKind.Utc));

        var data = await ReadPeriodAsync(patient.Id, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31));

        data.PeriodDays.Should().Be(31);
        data.DaysWithRecords.Should().Be(3);
        data.SymptomEntries.Should().HaveCount(2);
    }

    [SkippableFact]
    public async Task ReportData_PeriodBounds_FollowTheStartAndEndOfTheLimaDay()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        await SeedPatientProfileAsync(patient.Id);
        // 01/10 00:00 de Lima es 01/10 05:00 UTC: un segundo antes todavía es 30/09.
        await SeedMealAsync(patient.Id, new DateTime(2026, 10, 1, 4, 59, 59, DateTimeKind.Utc));
        await SeedMealAsync(patient.Id, new DateTime(2026, 10, 1, 5, 0, 0, DateTimeKind.Utc));
        // 05/10 23:59:59 de Lima es 06/10 04:59:59 UTC y entra; 06/10 05:00 UTC ya es 06/10 y queda fuera.
        await SeedSymptomAsync(patient.Id, SymptomType.Bloating, 40, new DateTime(2026, 10, 6, 4, 59, 59, DateTimeKind.Utc));
        await SeedSymptomAsync(patient.Id, SymptomType.Nausea, 30, new DateTime(2026, 10, 6, 5, 0, 0, DateTimeKind.Utc));

        var data = await ReadPeriodAsync(patient.Id, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 5));

        data.PeriodDays.Should().Be(5);
        data.Meals.Should().ContainSingle().Which.ConsumedAt.Should().Be(new DateTime(2026, 10, 1, 5, 0, 0, DateTimeKind.Utc));
        data.SymptomEntries.Should().ContainSingle().Which.SymptomType.Should().Be(nameof(SymptomType.Bloating));
        data.DaysWithRecords.Should().Be(2);
    }

    [SkippableFact]
    public async Task ReportData_ApprovedRecommendations_CarryFoodsWithActionTheNoteAndManualText()
    {
        SkipIfUnavailable();
        var patient = await SeedPatientAsync();
        await SeedPatientProfileAsync(patient.Id);
        var nutritionist = await SeedNutritionistAsync();
        await SeedMealAsync(patient.Id, DateTime.UtcNow.AddDays(-2));

        var (scope, db) = CreateDbScope();
        using (scope)
        {
            var modelVersionId = await db.ModelVersions.AsNoTracking().Select(v => v.Id).FirstAsync();
            var foods = await db.FoodItems.AsNoTracking().OrderBy(f => f.Name).Take(2).ToListAsync();
            var generatedAt = DateTime.UtcNow.AddDays(-3);
            var generated = Recommendation.Generate(
                patient.Id, modelVersionId, ConfidenceScore.Create(0.80m), ExplanationSource.LlmGenerated, "Explicación.",
                foods.Select(f => RecommendationItem.Create(f.Id, ActionType.Reduce, null, null)).ToList(),
                generatedAt, TimeSpan.FromHours(72));
            generated.MarkPendingReview(generatedAt);
            generated.Approve(nutritionist.Id, "Reducir por intolerancia a FODMAP", generatedAt.AddHours(1));
            var manual = Recommendation.CreateManual(
                patient.Id, nutritionist.Id, "Hidratación entre comidas", "Beber agua entre comidas.",
                ["Un vaso a media mañana."], "Indicada por estreñimiento leve.", null, DateTime.UtcNow.AddDays(-1));
            db.Recommendations.AddRange(generated, manual);
            await db.SaveChangesAsync();

            var data = await ReadAsync(patient.Id);

            data.ApprovedRecommendations.Should().HaveCount(2);
            var system = data.ApprovedRecommendations[0];
            system.IsManual.Should().BeFalse();
            system.ClinicalNote.Should().Be("Reducir por intolerancia a FODMAP");
            system.Foods.Select(f => f.FoodName).Should().BeEquivalentTo(foods.Select(f => f.Name));
            system.Foods.Should().OnlyContain(f => f.ActionType == nameof(ActionType.Reduce));
            var manualRow = data.ApprovedRecommendations[1];
            manualRow.IsManual.Should().BeTrue();
            manualRow.Title.Should().Be("Hidratación entre comidas");
            manualRow.Description.Should().Be("Beber agua entre comidas.");
            manualRow.Steps.Should().Equal("Un vaso a media mañana.");
            manualRow.ClinicalNote.Should().Be("Indicada por estreñimiento leve.");
        }
    }
}
