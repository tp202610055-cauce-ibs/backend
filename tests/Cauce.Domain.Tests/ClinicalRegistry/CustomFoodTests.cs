using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry.Exceptions;
using FluentAssertions;

namespace Cauce.Domain.Tests.ClinicalRegistry;

/// <summary>
/// Pruebas de invariantes y métodos de dominio de <see cref="CustomFood"/>.
/// </summary>
public sealed class CustomFoodTests
{
    private static readonly DateTime Now = new(2026, 6, 25, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PatientId = Guid.NewGuid();

    private static CustomFood CreateValid(decimal portion = 250m) =>
        CustomFood.Create(Guid.NewGuid(), PatientId, "Ensalada", portion, Now);

    [Fact]
    public void Create_ValidValues_CreatesEmptyCustomFood()
    {
        var customFood = CreateValid();

        customFood.PatientId.Should().Be(PatientId);
        customFood.Ingredients.Should().BeEmpty();
    }

    [Fact]
    public void Create_NonPositivePortion_Throws()
    {
        var act = () => CreateValid(portion: 0m);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void AddIngredient_NewFood_AddsIngredient()
    {
        var customFood = CreateValid();

        customFood.AddIngredient(Guid.NewGuid(), 100m);

        customFood.Ingredients.Should().HaveCount(1);
    }

    [Fact]
    public void AddIngredient_DuplicateFood_Throws()
    {
        var customFood = CreateValid();
        var foodId = Guid.NewGuid();
        customFood.AddIngredient(foodId, 100m);

        var act = () => customFood.AddIngredient(foodId, 50m);

        act.Should().Throw<DuplicateIngredientException>();
    }

    [Fact]
    public void RemoveIngredient_Missing_Throws()
    {
        var customFood = CreateValid();

        var act = () => customFood.RemoveIngredient(Guid.NewGuid());

        act.Should().Throw<IngredientNotFoundException>();
    }

    [Fact]
    public void UpdatePortionSize_NonPositive_Throws()
    {
        var customFood = CreateValid();

        var act = () => customFood.UpdatePortionSize(-5m);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void GetIngredientsWeightDelta_ComputesDifferenceWithPortion()
    {
        var customFood = CreateValid(portion: 250m);
        customFood.AddIngredient(Guid.NewGuid(), 100m);
        customFood.AddIngredient(Guid.NewGuid(), 100m);

        customFood.GetIngredientsWeightDelta().Should().Be(-50m);
    }
}
