using System.Security.Cryptography;
using System.Text;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Identity.UseCases.ValidatePasswordResetToken;
using Cauce.Domain.Identity;
using Cauce.Domain.Identity.Exceptions;
using FluentAssertions;
using NSubstitute;

namespace Cauce.Application.Tests.Identity;

/// <summary>
/// Pruebas del handler <see cref="ValidatePasswordResetTokenQueryHandler"/>: responde igual en todos
/// los casos en que el token no sirve y nunca lo consume.
/// </summary>
public sealed class ValidatePasswordResetTokenQueryHandlerTests
{
    private readonly IPasswordResetTokenRepository _tokenRepository = Substitute.For<IPasswordResetTokenRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();

    private const string PlainToken = "plain-token-value";

    private ValidatePasswordResetTokenQueryHandler CreateHandler() => new(_tokenRepository, _userRepository);

    [Fact]
    public async Task Handle_ValidToken_CompletesWithoutConsumingIt()
    {
        var user = CreateUser();
        var token = PasswordResetToken.Issue(Guid.NewGuid(), user.Id, Hash(PlainToken), DateTime.UtcNow, PasswordResetToken.Validity);
        _tokenRepository.FindByTokenHashAsync(Hash(PlainToken), Arg.Any<CancellationToken>()).Returns(token);
        _userRepository.FindByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var act = () => CreateHandler().Handle(new ValidatePasswordResetTokenQuery(PlainToken), CancellationToken.None);

        await act.Should().NotThrowAsync();
        token.IsUsed.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_UnknownToken_ThrowsInvalid()
    {
        _tokenRepository.FindByTokenHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((PasswordResetToken?)null);

        await AssertInvalidAsync(PlainToken);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_BlankToken_ThrowsInvalid(string? token)
    {
        await AssertInvalidAsync(token);
    }

    [Fact]
    public async Task Handle_UsedToken_ThrowsInvalid()
    {
        var user = CreateUser();
        var token = PasswordResetToken.Issue(Guid.NewGuid(), user.Id, Hash(PlainToken), DateTime.UtcNow, PasswordResetToken.Validity);
        token.Consume(DateTime.UtcNow);
        _tokenRepository.FindByTokenHashAsync(Hash(PlainToken), Arg.Any<CancellationToken>()).Returns(token);
        _userRepository.FindByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        await AssertInvalidAsync(PlainToken);
    }

    [Fact]
    public async Task Handle_ExpiredToken_ThrowsInvalidNotExpired()
    {
        var user = CreateUser();
        var token = PasswordResetToken.Issue(
            Guid.NewGuid(), user.Id, Hash(PlainToken), DateTime.UtcNow.AddMinutes(-31), PasswordResetToken.Validity);
        _tokenRepository.FindByTokenHashAsync(Hash(PlainToken), Arg.Any<CancellationToken>()).Returns(token);
        _userRepository.FindByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        await AssertInvalidAsync(PlainToken);
    }

    [Fact]
    public async Task Handle_MissingUser_ThrowsInvalid()
    {
        var token = PasswordResetToken.Issue(Guid.NewGuid(), Guid.NewGuid(), Hash(PlainToken), DateTime.UtcNow, PasswordResetToken.Validity);
        _tokenRepository.FindByTokenHashAsync(Hash(PlainToken), Arg.Any<CancellationToken>()).Returns(token);
        _userRepository.FindByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((User?)null);

        await AssertInvalidAsync(PlainToken);
    }

    private async Task AssertInvalidAsync(string? token)
    {
        var act = () => CreateHandler().Handle(new ValidatePasswordResetTokenQuery(token), CancellationToken.None);

        await act.Should().ThrowExactlyAsync<InvalidPasswordResetTokenException>();
    }

    private static User CreateUser() =>
        User.CreatePatient(Guid.NewGuid(), "kc-1", "p@cauce.local", "Paciente", 1, PatientCode.FromCorrelative(1));

    private static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
