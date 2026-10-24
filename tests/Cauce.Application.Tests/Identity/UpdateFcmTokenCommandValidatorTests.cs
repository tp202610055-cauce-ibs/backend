using Cauce.Application.Identity.UseCases.UpdateFcmToken;
using FluentAssertions;

namespace Cauce.Application.Tests.Identity;

/// <summary>
/// Pruebas del validador de <see cref="UpdateFcmTokenCommand"/>: el token es opcional y no supera el largo
/// de la columna, para que un valor excesivo dé 400 y no un error de base de datos.
/// </summary>
public sealed class UpdateFcmTokenCommandValidatorTests
{
    private readonly UpdateFcmTokenCommandValidator _validator = new();

    [Fact]
    public void Validate_NullToken_Passes()
    {
        _validator.Validate(new UpdateFcmTokenCommand(null)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_TokenAtTheColumnLimit_Passes()
    {
        var token = new string('a', UpdateFcmTokenCommandValidator.MaxTokenLength);

        _validator.Validate(new UpdateFcmTokenCommand(token)).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_TokenLongerThanTheColumn_Fails()
    {
        var token = new string('a', UpdateFcmTokenCommandValidator.MaxTokenLength + 1);

        _validator.Validate(new UpdateFcmTokenCommand(token)).IsValid.Should().BeFalse();
    }
}
