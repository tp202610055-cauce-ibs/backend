using System.Security.Cryptography;
using System.Text;
using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Identity.UseCases.ConfirmPasswordReset;
using Cauce.Domain.Identity;
using Cauce.Domain.Identity.Exceptions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Cauce.Application.Tests.Identity;

/// <summary>
/// Pruebas del handler <see cref="ConfirmPasswordResetCommandHandler"/>.
/// </summary>
public sealed class ConfirmPasswordResetCommandHandlerTests
{
    private readonly IPasswordResetTokenRepository _tokenRepository = Substitute.For<IPasswordResetTokenRepository>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IKeycloakAdminClient _keycloakAdminClient = Substitute.For<IKeycloakAdminClient>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IAuditLogger _auditLogger = Substitute.For<IAuditLogger>();
    private readonly ILogger<ConfirmPasswordResetCommandHandler> _logger =
        Substitute.For<ILogger<ConfirmPasswordResetCommandHandler>>();

    private const string PlainToken = "plain-token-value";

    private ConfirmPasswordResetCommandHandler CreateHandler() => new(
        _tokenRepository,
        _userRepository,
        _keycloakAdminClient,
        _unitOfWork,
        _auditLogger,
        _logger);

    [Fact]
    public async Task Handle_ValidToken_ResetsPasswordAndConsumesToken()
    {
        var user = User.CreatePatient(Guid.NewGuid(), "kc-1", "p@cauce.local", "Paciente", 1);
        var token = PasswordResetToken.Issue(Guid.NewGuid(), user.Id, Hash(PlainToken), DateTime.UtcNow, PasswordResetToken.Validity);
        _tokenRepository.FindByTokenHashAsync(Hash(PlainToken), Arg.Any<CancellationToken>()).Returns(token);
        _userRepository.FindByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        await CreateHandler().Handle(new ConfirmPasswordResetCommand(PlainToken, "NewPass1"), CancellationToken.None);

        await _keycloakAdminClient.Received(1).ResetPasswordAsync("kc-1", "NewPass1", Arg.Any<CancellationToken>());
        token.IsUsed.Should().BeTrue();
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnknownToken_ThrowsInvalid()
    {
        _tokenRepository.FindByTokenHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((PasswordResetToken?)null);

        var act = () => CreateHandler().Handle(new ConfirmPasswordResetCommand(PlainToken, "NewPass1"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidPasswordResetTokenException>();
    }

    [Fact]
    public async Task Handle_ExpiredToken_ThrowsExpired()
    {
        var user = User.CreatePatient(Guid.NewGuid(), "kc-1", "p@cauce.local", "Paciente", 1);
        var expiredToken = PasswordResetToken.Issue(
            Guid.NewGuid(), user.Id, Hash(PlainToken), DateTime.UtcNow.AddMinutes(-31), PasswordResetToken.Validity);
        _tokenRepository.FindByTokenHashAsync(Hash(PlainToken), Arg.Any<CancellationToken>()).Returns(expiredToken);

        var act = () => CreateHandler().Handle(new ConfirmPasswordResetCommand(PlainToken, "NewPass1"), CancellationToken.None);

        await act.Should().ThrowAsync<ExpiredPasswordResetTokenException>();
    }

    private static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
