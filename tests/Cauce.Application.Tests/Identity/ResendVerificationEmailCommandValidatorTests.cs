using Cauce.Application.Identity.UseCases.ResendVerificationEmail;
using FluentAssertions;

namespace Cauce.Application.Tests.Identity;

/// <summary>
/// Pruebas del validador de <see cref="ResendVerificationEmailCommand"/>. El endpoint es anónimo y la
/// clave de su rate limit se deriva del correo, así que un correo inválido no puede llegar al handler
/// (regla R10).
/// </summary>
public sealed class ResendVerificationEmailCommandValidatorTests
{
    private readonly ResendVerificationEmailCommandValidator _validator = new();

    [Fact]
    public void Validate_NullEmail_Fails()
    {
        var result = _validator.Validate(new ResendVerificationEmailCommand(null!));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_EmptyEmail_Fails()
    {
        var result = _validator.Validate(new ResendVerificationEmailCommand(string.Empty));

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("\t")]
    public void Validate_WhitespaceOnlyEmail_Fails(string email)
    {
        // NotEmpty de FluentValidation ya cubre el caso de solo espacios para cadenas, así que no hace
        // falta una regla Must adicional.
        var result = _validator.Validate(new ResendVerificationEmailCommand(email));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_EmailLongerThanTheRfcLimit_Fails()
    {
        var email = new string('a', 312) + "@cauce.local"; // 324 caracteres

        var result = _validator.Validate(new ResendVerificationEmailCommand(email));

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("sin-arroba")]
    [InlineData("con espacio@cauce.local")]
    [InlineData("@cauce.local")]
    public void Validate_MalformedEmail_Fails(string email)
    {
        var result = _validator.Validate(new ResendVerificationEmailCommand(email));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_ValidEmail_Passes()
    {
        var result = _validator.Validate(new ResendVerificationEmailCommand("paciente@cauce.local"));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ValidEmailAtTheLengthLimit_Passes()
    {
        var email = new string('a', 308) + "@cauce.local"; // 320 caracteres exactos

        var result = _validator.Validate(new ResendVerificationEmailCommand(email));

        result.IsValid.Should().BeTrue();
    }
}
