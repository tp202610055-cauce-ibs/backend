using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Identity.UseCases.RefreshToken;
using Cauce.Domain.Auditing.Enums;
using Cauce.Domain.Identity;
using Cauce.Domain.Identity.Exceptions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Cauce.Application.Tests.Identity;

/// <summary>
/// Pruebas del handler <see cref="RefreshTokenCommandHandler"/>.
/// </summary>
public sealed class RefreshTokenCommandHandlerTests
{
    private const string Subject = "kc-sub-1";
    private const string RefreshToken = "refresh-vigente";
    private const int PatientRoleId = 1;

    private readonly IKeycloakTokenClient _tokenClient = Substitute.For<IKeycloakTokenClient>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IAuditLogger _auditLogger = Substitute.For<IAuditLogger>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ILogger<RefreshTokenCommandHandler> _logger =
        Substitute.For<ILogger<RefreshTokenCommandHandler>>();

    private RefreshTokenCommandHandler CreateHandler() =>
        new(_tokenClient, _userRepository, _auditLogger, _unitOfWork, _logger);

    private static RefreshTokenCommand Command() => new(RefreshToken, "cauce-mobile");

    private void GivenKeycloakRefreshes()
    {
        _tokenClient
            .RefreshAsync(RefreshToken, "cauce-mobile", Arg.Any<CancellationToken>())
            .Returns(new KeycloakTokenResult("nuevo-access", "nuevo-refresh", 900, 2592000, "Bearer", Subject));
    }

    private User GivenLocalUser()
    {
        var user = User.CreatePatient(Guid.NewGuid(), Subject, "p@cauce.local", "Paciente Demo", PatientRoleId);
        _userRepository.FindByKeycloakIdAsync(Subject, Arg.Any<CancellationToken>()).Returns(user);
        _userRepository.GetRoleNameAsync(PatientRoleId, Arg.Any<CancellationToken>()).Returns(UserRoles.Patient);
        return user;
    }

    [Fact]
    public async Task Handle_ValidRefreshToken_ReturnsNewTokensAndUser()
    {
        GivenKeycloakRefreshes();
        var user = GivenLocalUser();

        var result = await CreateHandler().Handle(Command(), CancellationToken.None);

        result.AccessToken.Should().Be("nuevo-access");
        result.RefreshToken.Should().Be("nuevo-refresh");
        result.User.UserId.Should().Be(user.Id);
        result.User.KeycloakId.Should().Be(Subject);
        result.User.Role.Should().Be(UserRoles.Patient);
    }

    [Fact]
    public async Task Handle_ValidRefreshToken_AuditsTokenRefreshWithExplicitActor()
    {
        GivenKeycloakRefreshes();
        var user = GivenLocalUser();

        await CreateHandler().Handle(Command(), CancellationToken.None);

        // El endpoint es anónimo: sin actor explícito la fila quedaría sin trazabilidad del acceso.
        await _auditLogger.Received(1).LogAsync(
            AuditActionType.TokenRefresh,
            nameof(User),
            Arg.Is<Guid?>(user.Id),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Is<Guid?>(user.Id),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_InvalidRefreshToken_ThrowsAndAuditsFailedTokenRefresh()
    {
        _tokenClient
            .RefreshAsync(RefreshToken, "cauce-mobile", Arg.Any<CancellationToken>())
            .Returns<KeycloakTokenResult>(_ => throw new InvalidRefreshTokenException());

        var act = async () => await CreateHandler().Handle(Command(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidRefreshTokenException>();
        await _auditLogger.Received(1).LogAsync(
            AuditActionType.FailedTokenRefresh,
            nameof(User),
            Arg.Is<Guid?>((Guid?)null),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Is<Guid?>((Guid?)null),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ValidRefreshButUserLocalMissing_ThrowsUserLocalMissingException()
    {
        GivenKeycloakRefreshes();
        _userRepository.FindByKeycloakIdAsync(Subject, Arg.Any<CancellationToken>()).Returns((User?)null);

        var act = async () => await CreateHandler().Handle(Command(), CancellationToken.None);

        await act.Should().ThrowAsync<UserLocalMissingException>();
        await _auditLogger.DidNotReceive().LogAsync(
            AuditActionType.TokenRefresh,
            Arg.Any<string>(),
            Arg.Any<Guid?>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<Guid?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_TokenWithoutSubject_ThrowsUserLocalMissingException()
    {
        _tokenClient
            .RefreshAsync(RefreshToken, "cauce-mobile", Arg.Any<CancellationToken>())
            .Returns(new KeycloakTokenResult("nuevo-access", "nuevo-refresh", 900, 2592000, "Bearer", Subject: null));

        var act = async () => await CreateHandler().Handle(Command(), CancellationToken.None);

        await act.Should().ThrowAsync<UserLocalMissingException>();
    }
}
