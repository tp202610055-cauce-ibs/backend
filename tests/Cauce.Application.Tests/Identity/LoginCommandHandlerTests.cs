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
    private const int NutritionistRoleId = 2;

    private readonly IKeycloakTokenClient _tokenClient = Substitute.For<IKeycloakTokenClient>();
    private readonly IKeycloakAdminClient _adminClient = Substitute.For<IKeycloakAdminClient>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly INutritionistActivationService _activationService = Substitute.For<INutritionistActivationService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ILogger<LoginCommandHandler> _logger = Substitute.For<ILogger<LoginCommandHandler>>();

    private LoginCommandHandler CreateHandler() =>
        new(_tokenClient, _adminClient, _userRepository, _activationService, _unitOfWork, _logger);

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

        // Desde el fix 7 el handler sí resuelve la cuenta local en el camino de fallo, para poder
        // distinguir una contraseña incorrecta de un bloqueo por intentos fallidos. Lo que no debe
        // ocurrir es persistir nada ni emitir tokens.
        await _userRepository.Received(1).FindByEmailAsync(Email, Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidCredentials_DelegatesActivationBeforePersisting()
    {
        GivenKeycloakAuthenticates();
        var user = User.CreateNutritionist(Guid.NewGuid(), "kc-sub-2", Email, "Nutri Demo", NutritionistRoleId);
        _userRepository.FindByEmailAsync(Email, Arg.Any<CancellationToken>()).Returns(user);
        _userRepository.GetRoleNameAsync(NutritionistRoleId, Arg.Any<CancellationToken>()).Returns(UserRoles.Nutritionist);

        await CreateHandler().Handle(new LoginCommand(Email, "Correct123!", "cauce-mobile"), CancellationToken.None);

        // La activación tiene que quedar enrolada antes del SaveChanges, para viajar en la misma
        // transacción que la marca de último acceso (acta A51).
        Received.InOrder(() =>
        {
            _activationService.ActivateIfPendingAsync(user, NutritionistActivationTrigger.Login, Arg.Any<CancellationToken>());
            _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task Handle_InvalidCredentials_DoesNotAttemptActivation()
    {
        _tokenClient
            .LoginAsync(Email, "wrong", "cauce-mobile", Arg.Any<CancellationToken>())
            .Returns<KeycloakTokenResult>(_ => throw new InvalidCredentialsException());
        var user = User.CreateNutritionist(Guid.NewGuid(), "kc-sub-2", Email, "Nutri Demo", NutritionistRoleId);
        _userRepository.FindByEmailAsync(Email, Arg.Any<CancellationToken>()).Returns(user);

        var act = async () => await CreateHandler()
            .Handle(new LoginCommand(Email, "wrong", "cauce-mobile"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidCredentialsException>();
        await _activationService.DidNotReceiveWithAnyArgs()
            .ActivateIfPendingAsync(default!, default, default);
    }
}
