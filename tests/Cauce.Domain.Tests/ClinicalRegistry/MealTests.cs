using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry.Enums;
using Cauce.Domain.ClinicalRegistry.Exceptions;
using FluentAssertions;

namespace Cauce.Domain.Tests.ClinicalRegistry;

/// <summary>
/// Pruebas de invariantes y métodos de dominio de <see cref="Meal"/>.
/// </summary>
public sealed class MealTests
{
    private static readonly DateTime Now = new(2026, 6, 25, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PatientId = Guid.NewGuid();

    private static MealItemInput FoodItemInput() => new(Guid.NewGuid(), null, 100m, MeasurementUnit.Grams);

    private static Meal Register(IEnumerable<MealItemInput> items, DateTime? consumedAt = null) =>
        Meal.Register(Guid.NewGuid(), Guid.NewGuid(), PatientId, MealTime.Lunch, consumedAt ?? Now, Now, items, Now);

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(50)]
    public void Register_ValidItemCount_CreatesMeal(int count)
    {
        var items = Enumerable.Range(0, count).Select(_ => FoodItemInput()).ToList();

        var meal = Register(items);

        meal.GetItemCount().Should().Be(count);
        meal.SyncStatus.Should().Be(SyncStatus.SyncCompleted);
    }

    [Fact]
    public void Register_ZeroItems_Throws()
    {
        var act = () => Register(Array.Empty<MealItemInput>());

        act.Should().Throw<InvalidMealRegistrationException>();
    }

    [Fact]
    public void Register_TooManyItems_Throws()
    {
        var items = Enumerable.Range(0, 51).Select(_ => FoodItemInput()).ToList();

        var act = () => Register(items);

        act.Should().Throw<InvalidMealRegistrationException>();
    }

    [Fact]
    public void Register_ItemWithBothFoodAndCustomFood_Throws()
    {
        var items = new[] { new MealItemInput(Guid.NewGuid(), Guid.NewGuid(), 100m, MeasurementUnit.Grams) };

        var act = () => Register(items);

        act.Should().Throw<InvalidMealRegistrationException>();
    }

    [Fact]
    public void Register_ItemWithNeitherFoodNorCustomFood_Throws()
    {
        var items = new[] { new MealItemInput(null, null, 100m, MeasurementUnit.Grams) };

        var act = () => Register(items);

        act.Should().Throw<InvalidMealRegistrationException>();
    }

    [Fact]
    public void Register_FutureConsumedAt_Throws()
    {
        var act = () => Register(new[] { FoodItemInput() }, consumedAt: Now.AddHours(1));

        act.Should().Throw<InvalidMealRegistrationException>();
    }
}
