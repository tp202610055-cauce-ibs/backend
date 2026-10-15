using Cauce.Domain.ClinicalRegistry.Enums;
using Cauce.Infrastructure.ClinicalRegistry;
using Cauce.Infrastructure.Persistence.Seeders;
using FluentAssertions;

namespace Cauce.Infrastructure.Tests.Persistence;

/// <summary>
/// Pruebas del generador del historial de volumen. Es una función pura: se fija el instante (mediodía de
/// Lima de una fecha pasada) y el paciente, y cada propiedad del plan se verifica sin base de datos.
/// </summary>
public sealed class VolumeHistoryGeneratorTests
{
    private static readonly DateTime Now = new(2026, 9, 15, 17, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PatientId = Guid.Parse("7d1f3b4e-2a9c-4c1e-9b8f-0a1b2c3d4e5f");
    private static readonly IReadOnlyList<CatalogFood> Catalog = FoodItemsSeeder.CatalogFoods;
    private static readonly VolumeHistoryPlan Plan = VolumeHistoryGenerator.Generate(Now, PatientId, Catalog);

    private static DateTime Lima(DateTime utc) => VolumeHistoryGenerator.ToLima(utc);

    private static DateOnly LimaDate(DateTime utc) => DateOnly.FromDateTime(Lima(utc));

    private static DateOnly Today => LimaDate(Now);

    [Fact]
    public void Generate_ProducesExactTotals()
    {
        Plan.Meals.Should().HaveCount(450);
        Plan.Symptoms.Should().HaveCount(180);
    }

    [Fact]
    public void Generate_KeepsEveryRowBetweenDay88AndNow()
    {
        var oldest = Today.AddDays(-88);
        Plan.Meals.Should().OnlyContain(meal => LimaDate(meal.ConsumedAt) >= oldest && meal.ConsumedAt <= Now && meal.ClientCreatedAt <= Now);
        Plan.Symptoms.Should().OnlyContain(symptom => LimaDate(symptom.OccurredAt) >= oldest && symptom.OccurredAt <= Now && symptom.ClientCreatedAt <= Now);
        Plan.Meals.Should().OnlyContain(meal => meal.ClientCreatedAt >= meal.ConsumedAt);
        Plan.Symptoms.Should().OnlyContain(symptom => symptom.ClientCreatedAt >= symptom.OccurredAt);
    }

    [Fact]
    public void Generate_PlacesTodayRowsMinutesBeforeNow()
    {
        Plan.Meals.Where(meal => LimaDate(meal.ConsumedAt) == Today).Select(meal => meal.ConsumedAt)
            .Should().BeEquivalentTo(new[] { Now.AddMinutes(-25), Now.AddMinutes(-12) });
        Plan.Symptoms.Where(symptom => LimaDate(symptom.OccurredAt) == Today).Select(symptom => symptom.OccurredAt)
            .Should().BeEquivalentTo(new[] { Now.AddMinutes(-8) });
    }

    [Theory]
    [InlineData(0, 29, false)]
    [InlineData(0, 30, true)]
    public void CanSeedAt_GuardsTheFirstHalfHourOfLima(int hour, int minute, bool expected)
    {
        var utc = VolumeHistoryGenerator.FromLima(new DateTime(2026, 9, 15, hour, minute, 0));

        VolumeHistoryGenerator.CanSeedAt(utc).Should().Be(expected);
    }

    [Theory]
    [InlineData(0, 30)]
    [InlineData(23, 59)]
    public void Generate_AtEitherEndOfTheLimaDay_KeepsTotalsAndNothingInTheFuture(int hour, int minute)
    {
        var now = VolumeHistoryGenerator.FromLima(new DateTime(2026, 9, 15, hour, minute, 0));

        var plan = VolumeHistoryGenerator.Generate(now, PatientId, Catalog);

        plan.Meals.Should().HaveCount(450);
        plan.Symptoms.Should().HaveCount(180);
        plan.Meals.Should().OnlyContain(meal => meal.ConsumedAt <= now && meal.ClientCreatedAt <= now);
        plan.Symptoms.Should().OnlyContain(symptom => symptom.OccurredAt <= now && symptom.ClientCreatedAt <= now);
        var today = LimaDate(now);
        plan.Meals.Count(meal => LimaDate(meal.ConsumedAt) == today).Should().Be(2);
        plan.Symptoms.Count(symptom => LimaDate(symptom.OccurredAt) == today).Should().Be(1);
    }

    [Fact]
    public void Generate_BeforeHalfPastMidnightInLima_Throws()
    {
        var utc = VolumeHistoryGenerator.FromLima(new DateTime(2026, 9, 15, 0, 29, 0));

        var act = () => VolumeHistoryGenerator.Generate(utc, PatientId, Catalog);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Generate_SpreadsMealsWithEmptyAndBusyDays()
    {
        var days = VolumeHistoryManifest.Days(Plan).Where(day => day.LimaDate < Today).ToList();
        days.Should().HaveCount(88);

        var empty = days.Where(day => day.Meals == 0 && day.Symptoms == 0).Select(day => day.LimaDate).ToList();
        empty.Should().HaveCountGreaterThanOrEqualTo(5);
        empty.Should().Contain(day => empty.Contains(day.AddDays(1)) && empty.Contains(day.AddDays(2)));
        days.Count(day => day.Meals >= 8).Should().BeGreaterThanOrEqualTo(3);
        days.Where(day => day.Meals is > 0 and < 8).Should().OnlyContain(day => day.Meals >= 3 && day.Meals <= 7);
    }

    [Fact]
    public void Generate_MatchesMealTimeWithLimaHour()
    {
        foreach (var meal in Plan.Meals)
        {
            var minutes = (int)Lima(meal.ConsumedAt).TimeOfDay.TotalMinutes;
            var expected = minutes switch
            {
                >= 390 and <= 570 => MealTime.Breakfast,
                >= 720 and <= 900 => MealTime.Lunch,
                >= 1110 and <= 1350 => MealTime.Dinner,
                _ => MealTime.Snack
            };
            meal.MealTime.Should().Be(expected);
        }
    }

    [Fact]
    public void Generate_RegistersMostMealsSoonAndSomeLateOrAcrossMidnight()
    {
        var delays = Plan.Meals.Select(meal => meal.ClientCreatedAt - meal.ConsumedAt).ToList();
        delays.Count(delay => delay <= TimeSpan.FromMinutes(40)).Should().BeGreaterThan(225);
        delays.Count(delay => delay >= TimeSpan.FromHours(10) && delay <= TimeSpan.FromHours(14)).Should().BeGreaterThanOrEqualTo(23);

        Plan.Meals.Count(meal => Lima(meal.ConsumedAt).Hour == 23 && LimaDate(meal.ClientCreatedAt) == LimaDate(meal.ConsumedAt).AddDays(1))
            .Should().BeGreaterThanOrEqualTo(6);

        var yesterdayLate = VolumeHistoryGenerator.FromLima(Today.AddDays(-1).ToDateTime(new TimeOnly(23, 40)));
        var todayEarly = VolumeHistoryGenerator.FromLima(Today.ToDateTime(new TimeOnly(0, 10)));
        Plan.Meals.Should().Contain(meal => meal.ConsumedAt == yesterdayLate && meal.ClientCreatedAt == todayEarly);
    }

    [Fact]
    public void Generate_PutsATieGroupOverEveryPageCut()
    {
        AssertCutsInsideGroups(Plan.Meals.Select(meal => (meal.ClientCreatedAt, meal.Id)).ToList(), cuts: 4, extraOnTop: 0);
        AssertCutsInsideGroups(Plan.Meals.Select(meal => (meal.ClientCreatedAt, meal.Id)).ToList(), cuts: 4, extraOnTop: 4);
        AssertCutsInsideGroups(Plan.Symptoms.Select(symptom => (symptom.ClientCreatedAt, symptom.Id)).ToList(), cuts: 1, extraOnTop: 0);
        AssertCutsInsideGroups(Plan.Symptoms.Select(symptom => (symptom.ClientCreatedAt, symptom.Id)).ToList(), cuts: 1, extraOnTop: 4);

        Plan.TieGroups.Should().HaveCount(5);
        var tied = Plan.TieGroups.Select(group => group.ClientCreatedAt).ToHashSet();
        Plan.Meals.Where(meal => !tied.Contains(meal.ClientCreatedAt)).Select(meal => meal.ClientCreatedAt).Should().OnlyHaveUniqueItems();
        Plan.Symptoms.Where(symptom => !tied.Contains(symptom.ClientCreatedAt)).Select(symptom => symptom.ClientCreatedAt).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Generate_UsesOnlyCatalogFoodsWithPeruvianPreference()
    {
        var names = Catalog.ToDictionary(food => food.Name);
        var catalogItems = Plan.Meals.SelectMany(meal => meal.Items).Where(item => item.FoodName is not null).ToList();

        catalogItems.Should().OnlyContain(item => names.ContainsKey(item.FoodName!));
        catalogItems.Should().NotContain(item => item.FoodName == VolumeHistoryGenerator.ProbeFoodName);
        var peruvianShare = catalogItems.Count(item => names[item.FoodName!].IsPeruvian) / (double)catalogItems.Count;
        peruvianShare.Should().BeInRange(0.55, 0.65);

        Plan.Meals.Should().OnlyContain(meal => meal.Items.Count >= 1 && meal.Items.Count <= 6 && meal.Items.Count != 5);
        Plan.Meals.Count(meal => meal.Items.Count == 6).Should().BeGreaterThanOrEqualTo(5);
    }

    [Fact]
    public void Generate_UsesEveryUnitWithMostlyGrams()
    {
        var items = Plan.Meals.SelectMany(meal => meal.Items).ToList();

        items.Select(item => item.Unit).Distinct().Should().BeEquivalentTo(Enum.GetValues<MeasurementUnit>());
        items.Count(item => item.Unit == MeasurementUnit.Grams).Should().BeGreaterThan(items.Count / 2);
        items.Where(item => item.Unit is MeasurementUnit.Cups or MeasurementUnit.Units or MeasurementUnit.Tablespoons)
            .Should().OnlyContain(item => item.Quantity == 0.5m || item.Quantity == 1m || item.Quantity == 1.5m || item.Quantity == 2m);
        items.Select(item => item.Quantity).Should().Contain(0.5m).And.Contain(1.5m);
    }

    [Fact]
    public void Generate_DistributesFodmapAsTheServerReadsIt()
    {
        var aggregator = new FodmapAggregator();
        var levels = Catalog.ToDictionary(food => food.Name, food => food.Level);
        var aggregated = Plan.Meals
            .Select(meal => aggregator.AggregateForMeal(meal.Items
                .Where(item => item.FoodName is not null)
                .Select(item => (levels[item.FoodName!], item.Quantity))))
            .ToList();

        Share(aggregated, FodmapLevel.Low).Should().BeInRange(0.55, 0.65);
        Share(aggregated, FodmapLevel.Moderate).Should().BeInRange(0.20, 0.30);
        Share(aggregated, FodmapLevel.High).Should().BeInRange(0.10, 0.20);
    }

    [Fact]
    public void Generate_PlansThreeCustomFoodsUsedInSomeMeals()
    {
        var names = Catalog.Select(food => food.Name).ToHashSet();
        Plan.CustomFoods.Should().HaveCount(3);
        Plan.CustomFoods.Select(food => food.Name).Should().OnlyHaveUniqueItems();
        Plan.CustomFoods.Should().OnlyContain(food => food.Ingredients.Count >= 2 && food.Ingredients.Count <= 3
            && food.Ingredients.All(ingredient => names.Contains(ingredient.FoodName)));

        var withCustom = Plan.Meals.Count(meal => meal.Items.Any(item => item.CustomFoodIndex is not null));
        (withCustom / 450.0).Should().BeInRange(0.06, 0.10);
        Plan.Meals.Count(meal => meal.Items.All(item => item.CustomFoodIndex is not null)).Should().BeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public void Generate_ReportsAllNineSymptomTypesWithMixedIbsWeights()
    {
        var byType = Plan.Symptoms.GroupBy(symptom => symptom.Type).ToDictionary(group => group.Key, group => group.Count());

        byType.Keys.Should().BeEquivalentTo(Enum.GetValues<SymptomType>());
        byType.OrderByDescending(pair => pair.Value).Take(2).Select(pair => pair.Key)
            .Should().BeEquivalentTo(new[] { SymptomType.AbdominalPain, SymptomType.Bloating });

        Plan.Symptoms.Should().OnlyContain(symptom => symptom.Intensity >= 1 && symptom.Intensity <= 100);
        Plan.Symptoms.Select(symptom => symptom.Intensity).Should().Contain(1).And.Contain(100);
        Plan.Symptoms.Count(symptom => symptom.Intensity > 80).Should().BeGreaterThanOrEqualTo(10);
    }

    [Fact]
    public void Generate_AssociatesSymptomsWithTheHandlerRule()
    {
        foreach (var symptom in Plan.Symptoms)
        {
            var windowStart = symptom.ClientCreatedAt.AddHours(-4);
            var expected = Plan.Meals
                .Where(meal => meal.ClientCreatedAt > windowStart && meal.ClientCreatedAt <= symptom.ClientCreatedAt)
                .OrderByDescending(meal => meal.ClientCreatedAt)
                .FirstOrDefault();
            (symptom.AssociatedMealId is null).Should().Be(expected is null);
            if (expected is not null)
            {
                Plan.Meals.Single(meal => meal.Id == symptom.AssociatedMealId).ClientCreatedAt.Should().Be(expected.ClientCreatedAt);
            }
        }

        var share = Plan.Symptoms.Count(symptom => symptom.AssociatedMealId is not null) / 180.0;
        share.Should().BeInRange(0.60, 0.70);
    }

    [Fact]
    public void Generate_SameInputs_ProduceTheSamePlan()
    {
        var again = VolumeHistoryGenerator.Generate(Now, PatientId, Catalog);

        again.Should().BeEquivalentTo(Plan, options => options.WithStrictOrdering());
        var reordered = VolumeHistoryGenerator.Generate(Now, PatientId, Catalog.Reverse().ToList());
        reordered.Should().BeEquivalentTo(Plan, options => options.WithStrictOrdering());
        Plan.Meals[0].ClientGuid.Should().Be(VolumeHistoryGenerator.DeriveId(PatientId, "meal-client", 0));
        Plan.Symptoms[0].ClientGuid.Should().Be(VolumeHistoryGenerator.DeriveId(PatientId, "symptom-client", 0));
    }

    [Fact]
    public void Manifest_DayRowsAddUpToTheTotals()
    {
        var days = VolumeHistoryManifest.Days(Plan);

        days.Should().HaveCount(89);
        days.Sum(day => day.Meals).Should().Be(450);
        days.Sum(day => day.Symptoms).Should().Be(180);
        VolumeHistoryManifest.Build(Plan).Should().Contain("Totals: 450 meals, 180 symptoms");
    }

    private static double Share(IReadOnlyCollection<FodmapLevel> levels, FodmapLevel level) =>
        levels.Count(candidate => candidate == level) / (double)levels.Count;

    private static void AssertCutsInsideGroups(List<(DateTime CreatedAt, Guid Id)> rows, int cuts, int extraOnTop)
    {
        var newer = Enumerable.Range(1, extraOnTop).Select(offset => (CreatedAt: Now.AddSeconds(offset), Id: Guid.NewGuid()));
        List<(DateTime CreatedAt, Guid Id)> ordered = rows.Concat(newer)
            .OrderBy(row => row, Comparer<(DateTime CreatedAt, Guid Id)>.Create(
                (first, second) => VolumeHistoryGenerator.CompareServerOrder(first.CreatedAt, first.Id, second.CreatedAt, second.Id)))
            .ToList();

        for (var cut = 1; cut <= cuts; cut++)
        {
            var lastOnPage = ordered[(cut * 100) - 1];
            var firstOnNext = ordered[cut * 100];
            firstOnNext.CreatedAt.Should().Be(lastOnPage.CreatedAt, $"el corte {cut * 100}|{(cut * 100) + 1} cae dentro de un grupo empatado");
        }
    }
}
