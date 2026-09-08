using Cauce.Application.Patients.UseCases.AssignNutritionist;
using FluentAssertions;

namespace Cauce.Application.Tests.Patients;

/// <summary>
/// Pruebas del validador de <see cref="AssignNutritionistCommand"/>. Las reglas replican las que el
/// registro aplica al código de invitación, para que un mismo código sea válido o inválido igual en los
/// dos flujos (regla R10).
/// </summary>
public sealed class AssignNutritionistCommandValidatorTests
{
    private readonly AssignNutritionistCommandValidator _validator = new();

    [Fact]
    public void Validate_NullCode_Fails()
    {
        _validator.Validate(new AssignNutritionistCommand(null!)).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_EmptyCode_Fails()
    {
        _validator.Validate(new AssignNutritionistCommand(string.Empty)).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("\t")]
    public void Validate_WhitespaceOnlyCode_Fails(string code)
    {
        _validator.Validate(new AssignNutritionistCommand(code)).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("ABC123")]      // 6, por debajo del mínimo
    [InlineData("ABCDEFGHIJKLMNOPQRSTU")] // 21, por encima del máximo
    public void Validate_CodeOutsideTheLengthRange_Fails(string code)
    {
        _validator.Validate(new AssignNutritionistCommand(code)).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("ABCD-123")]
    [InlineData("abcd1234")]
    [InlineData("ABCD 123")]
    public void Validate_CodeWithInvalidCharacters_Fails(string code)
    {
        _validator.Validate(new AssignNutritionistCommand(code)).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("ABCDEFGH")]                // 8, el mínimo
    [InlineData("ABCD1234EFGH5678IJKL")]    // 20, el máximo
    public void Validate_WellFormedCode_Passes(string code)
    {
        _validator.Validate(new AssignNutritionistCommand(code)).IsValid.Should().BeTrue();
    }
}
