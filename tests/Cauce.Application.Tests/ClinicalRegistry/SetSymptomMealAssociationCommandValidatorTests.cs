using Cauce.Application.ClinicalRegistry.UseCases.SetSymptomMealAssociation;
using FluentAssertions;

namespace Cauce.Application.Tests.ClinicalRegistry;

/// <summary>
/// Pruebas del validador de <see cref="SetSymptomMealAssociationCommand"/>. Un <c>MealId</c> nulo es la
/// forma de desvincular y debe pasar; el GUID vacío no identifica ninguna comida y debe fallar.
/// </summary>
public sealed class SetSymptomMealAssociationCommandValidatorTests
{
    private readonly SetSymptomMealAssociationCommandValidator _validator = new();

    [Fact]
    public void Validate_WithMeal_Passes()
    {
        _validator.Validate(new SetSymptomMealAssociationCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()))
            .IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_NullMeal_Passes()
    {
        _validator.Validate(new SetSymptomMealAssociationCommand(Guid.NewGuid(), null, Guid.NewGuid()))
            .IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyMeal_Fails()
    {
        _validator.Validate(new SetSymptomMealAssociationCommand(Guid.NewGuid(), Guid.Empty, Guid.NewGuid()))
            .IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_EmptyIdempotencyKey_Fails()
    {
        _validator.Validate(new SetSymptomMealAssociationCommand(Guid.NewGuid(), null, Guid.Empty))
            .IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_EmptySymptom_Fails()
    {
        _validator.Validate(new SetSymptomMealAssociationCommand(Guid.Empty, null, Guid.NewGuid()))
            .IsValid.Should().BeFalse();
    }
}
