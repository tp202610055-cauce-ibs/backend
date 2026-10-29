using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Identity.Services;
using Cauce.Application.Identity.UseCases.RefreshToken;
using Cauce.Domain.Identity;
using Cauce.Domain.Identity.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Cauce.Application.Tests.Identity;

/// <summary>
/// Sincronización de <c>emailVerified</c> desde Keycloak al renovar la sesión: solo si el correo local aún no está
/// verificado, y sin que un fallo de Keycloak haga fallar la renovación.
/// </summary>
public sealed class RefreshTokenCommandHandlerEmailSyncTests
{
    private const string Subject = "kc-sub-1";
    private const string RefreshToken = "refresh-vigente";
    private const int PatientRoleId = 1;

    private readonly IKeycloakTokenClient _tokenClient = Substitute.For<IKeycloakTokenClient>();
    private readonly IKeycloakAdminClient _adminClient = Substitute.For<IKeycloakAdminClient>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IAuditLogger _auditLogger = Substitute.For<IAuditLogger>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ILogger<RefreshTokenCommandHandler> _logger = Substitute.For<ILogger<RefreshTokenCommandHandler>>();

    private RefreshTokenCommandHandler CreateHandler() =>
        new(_tokenClient, _userRepository, new EmailVerificationSync(_adminClient, NullLogger<EmailVerificationSync>.Instance),
            _auditLogger, _unitOfWork, _logger);

    private static RefreshTokenCommand Command() => new(RefreshToken, "cauce-mobile");

    private User GivenSession(bool emailVerified)
    {
        _tokenClient
            .RefreshAsync(RefreshToken, "cauce-mobile", Arg.Any<CancellationToken>())
            .Returns(new KeycloakTokenResult("nuevo-access", "nuevo-refresh", 900, 2592000, "Bearer", Subject));
        var user = User.CreatePatient(Guid.NewGuid(), Subject, "p@cauce.local", "Paciente Demo", PatientRoleId, PatientCode.FromCorrelative(1));
        if (emailVerified)
        {
            user.VerifyEmail();
        }

        _userRepository.FindByKeycloakIdAsync(Subject, Arg.Any<CancellationToken>()).Returns(user);
        _userRepository.GetRoleNameAsync(PatientRoleId, Arg.Any<CancellationToken>()).Returns(UserRoles.Patient);
        return user;
    }

    [Fact]
    public async Task Handle_UnverifiedLocallyAndVerifiedInKeycloak_ActivatesAndReturnsVerified()
    {
        var user = GivenSession(emailVerified: false);
        _adminClient.GetUserEmailVerifiedAsync(Subject, Arg.Any<CancellationToken>()).Returns(true);

        var result = await CreateHandler().Handle(Command(), CancellationToken.None);

        result.User.EmailVerified.Should().BeTrue();
        user.Status.Should().Be(UserStatus.Active);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnverifiedInBothSides_StaysPendingAndReturnsUnverified()
    {
        var user = GivenSession(emailVerified: false);
        _adminClient.GetUserEmailVerifiedAsync(Subject, Arg.Any<CancellationToken>()).Returns(false);

        var result = await CreateHandler().Handle(Command(), CancellationToken.None);

        result.User.EmailVerified.Should().BeFalse();
        user.Status.Should().Be(UserStatus.PendingActivation);
    }

    [Fact]
    public async Task Handle_KeycloakAdminApiFails_RenewsWithTheLocalValue()
    {
        var user = GivenSession(emailVerified: false);
        _adminClient
            .GetUserEmailVerifiedAsync(Subject, Arg.Any<CancellationToken>())
            .Returns<bool>(_ => throw new HttpRequestException("Admin API caída"));

        var result = await CreateHandler().Handle(Command(), CancellationToken.None);

        result.AccessToken.Should().Be("nuevo-access");
        result.User.EmailVerified.Should().BeFalse();
        user.Status.Should().Be(UserStatus.PendingActivation);
    }

    [Fact]
    public async Task Handle_AlreadyVerified_DoesNotCallKeycloak()
    {
        GivenSession(emailVerified: true);

        var result = await CreateHandler().Handle(Command(), CancellationToken.None);

        result.User.EmailVerified.Should().BeTrue();
        await _adminClient.DidNotReceiveWithAnyArgs().GetUserEmailVerifiedAsync(default!, default);
    }
}
