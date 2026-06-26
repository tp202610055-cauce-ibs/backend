using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Identity.UseCases.GenerateInvitationCode;
using Cauce.Domain.Identity;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Cauce.Application.Tests.Identity;

/// <summary>
/// Pruebas del handler <see cref="GenerateInvitationCodeCommandHandler"/>.
/// </summary>
public sealed class GenerateInvitationCodeCommandHandlerTests
{
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IInvitationCodeRepository _invitationCodeRepository = Substitute.For<IInvitationCodeRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IAuditLogger _auditLogger = Substitute.For<IAuditLogger>();
    private readonly ILogger<GenerateInvitationCodeCommandHandler> _logger =
        Substitute.For<ILogger<GenerateInvitationCodeCommandHandler>>();

    private GenerateInvitationCodeCommandHandler CreateHandler() => new(
        _userRepository,
        _invitationCodeRepository,
        _unitOfWork,
        _auditLogger,
        _logger);

    [Fact]
    public async Task Handle_Nutritionist_GeneratesAndPersistsCode()
    {
        var nutritionist = User.CreateNutritionist(Guid.NewGuid(), "kc-nutri", "n@cauce.local", "Nutri", 2);
        _userRepository.FindByKeycloakIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(nutritionist);
        _userRepository.GetRoleIdAsync(UserRoles.Nutritionist, Arg.Any<CancellationToken>()).Returns(2);
        _invitationCodeRepository.FindByCodeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((InvitationCode?)null);

        var result = await CreateHandler().Handle(new GenerateInvitationCodeCommand(Guid.NewGuid()), CancellationToken.None);

        result.Code.Should().NotBeNullOrWhiteSpace();
        result.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
        await _invitationCodeRepository.Received(1).AddAsync(Arg.Any<InvitationCode>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnknownUser_ThrowsUnauthorized()
    {
        _userRepository.FindByKeycloakIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((User?)null);
        _userRepository.GetRoleIdAsync(UserRoles.Nutritionist, Arg.Any<CancellationToken>()).Returns(2);

        var act = () => CreateHandler().Handle(new GenerateInvitationCodeCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Handle_NonNutritionistRole_ThrowsUnauthorized()
    {
        var patient = User.CreatePatient(Guid.NewGuid(), "kc-pat", "p@cauce.local", "Paciente", 1);
        _userRepository.FindByKeycloakIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(patient);
        _userRepository.GetRoleIdAsync(UserRoles.Nutritionist, Arg.Any<CancellationToken>()).Returns(2);

        var act = () => CreateHandler().Handle(new GenerateInvitationCodeCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }
}
