using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Common.Interfaces.Patients;
using Cauce.Application.Identity.UseCases.RegisterPatient;
using Cauce.Domain.Auditing.Enums;
using Cauce.Domain.Identity;
using Cauce.Domain.Identity.Exceptions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Cauce.Application.Tests.Identity;

/// <summary>
/// Pruebas del handler <see cref="RegisterPatientCommandHandler"/>.
/// </summary>
public sealed class RegisterPatientCommandHandlerTests
{
    private readonly IConsentService _consentService = Substitute.For<IConsentService>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IInvitationCodeRepository _invitationCodeRepository = Substitute.For<IInvitationCodeRepository>();
    private readonly IConsentRecordRepository _consentRecordRepository = Substitute.For<IConsentRecordRepository>();
    private readonly IKeycloakAdminClient _keycloakAdminClient = Substitute.For<IKeycloakAdminClient>();
    private readonly IPatientNutritionistAssignmentService _assignmentService =
        Substitute.For<IPatientNutritionistAssignmentService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IAuditLogger _auditLogger = Substitute.For<IAuditLogger>();
    private readonly ILogger<RegisterPatientCommandHandler> _logger = Substitute.For<ILogger<RegisterPatientCommandHandler>>();

    private RegisterPatientCommandHandler CreateHandler() => new(
        _consentService,
        _userRepository,
        _invitationCodeRepository,
        _consentRecordRepository,
        _keycloakAdminClient,
        _assignmentService,
        _unitOfWork,
        _auditLogger,
        _logger);

    private static RegisterPatientCommand ValidCommand() => new(
        "patient@cauce.local",
        "Paciente Uno",
        "Password1",
        "1.0",
        new string('a', 64),
        "127.0.0.1",
        null);

    [Fact]
    public async Task Handle_ValidRequest_PersistsUserConsentAndSendsVerifyEmail()
    {
        _consentService.VerifyHash(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        _userRepository.ExistsByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        _userRepository.GetRoleIdAsync(UserRoles.Patient, Arg.Any<CancellationToken>()).Returns(1);
        _keycloakAdminClient
            .CreateUserAsync(Arg.Any<string>(), Arg.Any<string>(), UserRoles.Patient, true, Arg.Any<CancellationToken>())
            .Returns("kc-id");

        var result = await CreateHandler().Handle(ValidCommand(), CancellationToken.None);

        result.UserId.Should().NotBeEmpty();
        result.EmailVerificationRequired.Should().BeTrue();
        await _userRepository.Received(1).AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        await _consentRecordRepository.Received(1).AddAsync(Arg.Any<ConsentRecord>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _auditLogger.Received(1).LogAsync(
            AuditActionType.Register, Arg.Any<string>(), Arg.Any<Guid?>(),
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(),
            Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
        await _keycloakAdminClient.Received(1).SendVerifyEmailAsync("kc-id", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ConsentHashMismatch_ThrowsAndDoesNotCallKeycloak()
    {
        _consentService.VerifyHash(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        var act = () => CreateHandler().Handle(ValidCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<ConsentTextMismatchException>();
        await _keycloakAdminClient.DidNotReceive().CreateUserAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DuplicateEmail_ThrowsDuplicateEmailException()
    {
        _consentService.VerifyHash(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        _userRepository.ExistsByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        var act = () => CreateHandler().Handle(ValidCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<DuplicateEmailException>();
    }

    [Fact]
    public async Task Handle_PersistenceFailsAfterKeycloakCreation_CompensatesByDeletingKeycloakUser()
    {
        _consentService.VerifyHash(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        _userRepository.ExistsByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        _userRepository.GetRoleIdAsync(UserRoles.Patient, Arg.Any<CancellationToken>()).Returns(1);
        _keycloakAdminClient
            .CreateUserAsync(Arg.Any<string>(), Arg.Any<string>(), UserRoles.Patient, true, Arg.Any<CancellationToken>())
            .Returns("kc-id");
        _userRepository.AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("db down"));

        var act = () => CreateHandler().Handle(ValidCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        await _keycloakAdminClient.Received(1).DeleteUserAsync("kc-id", Arg.Any<CancellationToken>());
    }
}
