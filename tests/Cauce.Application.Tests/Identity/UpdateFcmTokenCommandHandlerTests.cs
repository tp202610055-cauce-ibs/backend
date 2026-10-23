using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Identity.UseCases.UpdateFcmToken;
using Cauce.Domain.Auditing.Enums;
using Cauce.Domain.Identity;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Cauce.Application.Tests.Identity;

/// <summary>
/// Pruebas del handler de registro del token de FCM (TS10 CA01).
/// </summary>
public sealed class UpdateFcmTokenCommandHandlerTests
{
    private const int PatientRoleId = 1;

    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IAuditLogger _auditLogger = Substitute.For<IAuditLogger>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ILogger<UpdateFcmTokenCommandHandler> _logger =
        Substitute.For<ILogger<UpdateFcmTokenCommandHandler>>();

    private readonly User _user = User.CreatePatient(Guid.NewGuid(), "kc", "p@cauce.local", "Paciente", PatientRoleId, PatientCode.FromCorrelative(1));

    public UpdateFcmTokenCommandHandlerTests()
    {
        _currentUser.UserId.Returns(Guid.NewGuid());
        _userRepository.FindByKeycloakIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(_user);
        _userRepository.ListOthersWithFcmTokenAsync(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<User>());
    }

    private UpdateFcmTokenCommandHandler CreateHandler() =>
        new(_currentUser, _userRepository, _auditLogger, _unitOfWork, _logger);

    [Fact]
    public async Task Handle_RegistersTokenAuditsAndSaves()
    {
        await CreateHandler().Handle(new UpdateFcmTokenCommand("device-token-123"), CancellationToken.None);

        _user.FcmToken.Should().Be("device-token-123");
        await _auditLogger.Received(1).LogAsync(
            Arg.Is(AuditActionType.Update),
            Arg.Is("User"),
            Arg.Is<Guid?>(_user.Id),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Is<string?>(context => context != null && context.Contains("fcm_token_updated")),
            Arg.Any<Guid?>(),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NullToken_UnlinksToken()
    {
        _user.RegisterFcmToken("previous-token");

        await CreateHandler().Handle(new UpdateFcmTokenCommand(null), CancellationToken.None);

        _user.FcmToken.Should().BeNull();
        await _userRepository.DidNotReceiveWithAnyArgs().ListOthersWithFcmTokenAsync(default!, default, default);
    }

    [Fact]
    public async Task Handle_TokenHeldByAnotherAccount_UnlinksItFromThatAccountAndAuditsWithoutTheToken()
    {
        var previousOwner = User.CreatePatient(
            Guid.NewGuid(), "kc-2", "q@cauce.local", "Otro paciente", PatientRoleId, PatientCode.FromCorrelative(2));
        previousOwner.RegisterFcmToken("shared-device-token");
        _userRepository.ListOthersWithFcmTokenAsync("shared-device-token", _user.Id, Arg.Any<CancellationToken>())
            .Returns(new[] { previousOwner });

        await CreateHandler().Handle(new UpdateFcmTokenCommand("shared-device-token"), CancellationToken.None);

        _user.FcmToken.Should().Be("shared-device-token");
        previousOwner.FcmToken.Should().BeNull();
        await _auditLogger.Received(1).LogAsync(
            Arg.Is(AuditActionType.Update),
            Arg.Is("User"),
            Arg.Is<Guid?>(previousOwner.Id),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Is<string?>(context => context != null && context.Contains("fcm_token_moved_to_another_account")),
            Arg.Any<Guid?>(),
            Arg.Any<CancellationToken>());
        await _auditLogger.DidNotReceive().LogAsync(
            Arg.Any<AuditActionType>(),
            Arg.Any<string>(),
            Arg.Any<Guid?>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Is<string?>(context => context != null && context.Contains("shared-device-token")),
            Arg.Any<Guid?>(),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
