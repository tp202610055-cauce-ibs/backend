using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Identity.UseCases.Login;
using Cauce.Domain.Identity;
using Cauce.Domain.Identity.Exceptions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Cauce.Application.Tests.Identity;

/// <summary>
/// Pruebas del handler <see cref="LoginCommandHandler"/>.
/// </summary>
public sealed class LoginCommandHandlerTests
{
    private const string Email = "p@cauce.local";
    private const int PatientRoleId = 1;

    private readonly IKeycloakTokenClient _tokenClient = Substitute.For<IKeycloakTokenClient>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ILogger<LoginCommandHandler> _logger = Substitute.For<ILogger<LoginCommandHandler>>();

    private LoginCommandHandler CreateHandler() => new(_tokenClient, _userRepository, _unitOfWork, _logger);

    private void GivenKeycloakAuthenticates()
    {
        _tokenClient
            .LoginAsync(Email, "Correct123!", "cauce-mobile", Arg.Any<CancellationToken>())
            .Returns(new KeycloakTokenResult("access", "refresh", 900, 2592000, "Bearer"));
    }

    [Fact]
    public async Task Handle_ValidCredentials_ReturnsTokensAndUser()
    {
        GivenKeycloakAuthenticates();
        var user = User.CreatePatient(Guid.NewGuid(), "kc-sub-1", Email, "Paciente Demo", PatientRoleId);
        _userRepository.FindByEmailAsync(Email, Arg.Any<CancellationToken>()).Returns(user);
        _userRepository.GetRoleNameAsync(PatientRoleId, Arg.Any<CancellationToken>()).Returns(UserRoles.Patient);

        var result = await CreateHandler()
            .Handle(new LoginCommand(Email, "Correct123!", "cauce-mobile"), CancellationToken.None);

        result.AccessToken.Should().Be("access");
        result.RefreshToken.Should().Be("refresh");
        result.User.Should().BeEquivalentTo(new AuthenticatedUser(
            user.Id,
            "kc-sub-1",
            Email,
            UserRoles.Patient,
            "Paciente Demo",
            EmailVerified: false,
            IsInActivePilot: false));
    }

    [Fact]
    public async Task Handle_ValidCredentials_RegistersSuccessfulLoginAndPersists()
    {
        GivenKeycloakAuthenticates();
        var user = User.CreatePatient(Guid.NewGuid(), "kc-sub-1", Email, "Paciente Demo", PatientRoleId);
        _userRepository.FindByEmailAsync(Email, Arg.Any<CancellationToken>()).Returns(user);
        _userRepository.GetRoleNameAsync(PatientRoleId, Arg.Any<CancellationToken>()).Returns(UserRoles.Patient);

        await CreateHandler().Handle(new LoginCommand(Email, "Correct123!", "cauce-mobile"), CancellationToken.None);

        user.LastLoginAt.Should().NotBeNull();
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_KeycloakAuthenticatesButUserLocalMissing_ThrowsUserLocalMissingException()
    {
        GivenKeycloakAuthenticates();
        _userRepository.FindByEmailAsync(Email, Arg.Any<CancellationToken>()).Returns((User?)null);

        var act = async () => await CreateHandler()
            .Handle(new LoginCommand(Email, "Correct123!", "cauce-mobile"), CancellationToken.None);

        await act.Should().ThrowAsync<UserLocalMissingException>();
        // No debe persistirse nada de una sesión que no tiene cuenta local detrás.
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_InvalidCredentials_PropagatesInvalidCredentialsException()
    {
        _tokenClient
            .LoginAsync(Email, "wrong", "cauce-mobile", Arg.Any<CancellationToken>())
            .Returns<KeycloakTokenResult>(_ => throw new InvalidCredentialsException());

        var act = async () => await CreateHandler()
            .Handle(new LoginCommand(Email, "wrong", "cauce-mobile"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidCredentialsException>();
        await _userRepository.DidNotReceive().FindByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
