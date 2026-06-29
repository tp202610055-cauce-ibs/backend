using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry.Enums;
using FluentAssertions;

namespace Cauce.Domain.Tests.ClinicalRegistry;

/// <summary>
/// Pruebas de invariantes y métodos de dominio de <see cref="FoodItem"/>.
/// </summary>
public sealed class FoodItemTests
{
    private static readonly DateTime Now = new(2026, 6, 25, 0, 0, 0, DateTimeKind.Utc);

    private static FoodItem CreateValid(FodmapLevel level = FodmapLevel.Low) =>
        FoodItem.SeedEntry(Guid.NewGuid(), "Quinua", "cereales", 120m, 4.4m, 21.3m, 1.9m, 2.8m, level, null, true, Now);

    [Fact]
    public void SeedEntry_ValidValues_CreatesActiveEntry()
    {
        var food = CreateValid();

        food.IsActive.Should().BeTrue();
        food.Name.Should().Be("Quinua");
    }

    [Fact]
    public void SeedEntry_NegativeMacro_Throws()
    {
        var act = () => FoodItem.SeedEntry(Guid.NewGuid(), "Mal", "cereales", -1m, 0m, 0m, 0m, 0m, FodmapLevel.Low, null, false, Now);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(FodmapLevel.High, true)]
    [InlineData(FodmapLevel.Moderate, false)]
    [InlineData(FodmapLevel.Low, false)]
    public void IsHighFodmap_ForLevel_ReturnsExpected(FodmapLevel level, bool expected)
    {
        CreateValid(level).IsHighFodmap().Should().Be(expected);
    }

    [Fact]
    public void Deactivate_ThenReactivate_TogglesIsActive()
    {
        var food = CreateValid();

        food.Deactivate(Now.AddHours(1));
        food.IsActive.Should().BeFalse();

        food.Reactivate(Now.AddHours(2));
        food.IsActive.Should().BeTrue();
    }
}
