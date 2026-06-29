using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry.Enums;
using FluentAssertions;

namespace Cauce.Domain.Tests.ClinicalRegistry;

/// <summary>
/// Pruebas de invariantes y métodos de dominio de <see cref="Symptom"/>.
/// </summary>
public sealed class SymptomTests
{
    private static readonly DateTime Now = new(2026, 6, 25, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PatientId = Guid.NewGuid();

    private static Symptom Report(int intensity = 50) =>
        Symptom.Report(Guid.NewGuid(), Guid.NewGuid(), PatientId, SymptomType.Bloating, intensity, Now, Now, Now);

    [Fact]
    public void Report_ValidValues_CreatesUnassociatedSymptom()
    {
        var symptom = Report();

        symptom.HasMealAssociation.Should().BeFalse();
        symptom.AssociatedMealId.Should().BeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void Report_IntensityOutOfRange_Throws(int intensity)
    {
        var act = () => Report(intensity);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void AssociateWithMeal_SetsAssociation()
    {
        var symptom = Report();
        var mealId = Guid.NewGuid();

        symptom.AssociateWithMeal(mealId, Now);

        symptom.HasMealAssociation.Should().BeTrue();
        symptom.AssociatedMealId.Should().Be(mealId);
    }

    [Fact]
    public void ClearMealAssociation_RemovesAssociation()
    {
        var symptom = Report();
        symptom.AssociateWithMeal(Guid.NewGuid(), Now);

        symptom.ClearMealAssociation(Now);

        symptom.HasMealAssociation.Should().BeFalse();
        symptom.AssociatedMealId.Should().BeNull();
    }
}
