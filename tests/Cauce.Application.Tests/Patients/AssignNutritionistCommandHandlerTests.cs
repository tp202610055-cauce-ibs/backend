using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Common.Interfaces.Patients;
using Cauce.Application.Patients.UseCases.AssignNutritionist;
using Cauce.Domain.Auditing.Enums;
using Cauce.Domain.Identity;
using Cauce.Domain.Identity.Enums;
using Cauce.Domain.Identity.Exceptions;
using Cauce.Domain.Patients;
using Cauce.Domain.Patients.Exceptions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Cauce.Application.Tests.Patients;

/// <summary>
/// Pruebas del handler de canje de código de invitación post-registro (acta A41).
/// </summary>
/// <remarks>
/// El invariante central de estas pruebas es que <b>el código no se consume salvo en el caso feliz</b>:
/// cualquier rechazo debe dejarlo intacto para que el nutricionista pueda reutilizarlo o reemitirlo.
/// </remarks>
public sealed class AssignNutritionistCommandHandlerTests
{
    private const string Code = "ABCDEFGH";
    private const int PatientRoleId = 1;
    private const int NutritionistRoleId = 2;

    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IInvitationCodeRepository _invitationCodeRepository =
        Substitute.For<IInvitationCodeRepository>();
    private readonly INutritionistPatientRepository _nutritionistPatientRepository =
        Substitute.For<INutritionistPatientRepository>();
    private readonly IPatientNutritionistAssignmentService _assignmentService =
        Substitute.For<IPatientNutritionistAssignmentService>();
    private readonly IAuditLogger _auditLogger = Substitute.For<IAuditLogger>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ILogger<AssignNutritionistCommandHandler> _logger =
        Substitute.For<ILogger<AssignNutritionistCommandHandler>>();

    private AssignNutritionistCommandHandler CreateHandler() => new(
        _currentUserService,
        _userRepository,
        _invitationCodeRepository,
        _nutritionistPatientRepository,
        _assignmentService,
        _auditLogger,
        _unitOfWork,
        _logger);

    private static Task<AssignNutritionistResult> Act(AssignNutritionistCommandHandler handler) =>
        handler.Handle(new AssignNutritionistCommand(Code), CancellationToken.None);

    /// <summary>
    /// Arma el escenario feliz: paciente autenticado, sin asignación previa, con un código vigente cuyo
    /// nutricionista está activo.
    /// </summary>
    private (User Patient, User Nutritionist, InvitationCode Invitation) ArrangeHappyPath()
    {
        var patient = User.CreatePatient(Guid.NewGuid(), "kc-patient", "p@cauce.local", "Paciente", PatientRoleId);
        _currentUserService.UserId.Returns(Guid.NewGuid());
        _userRepository.FindByKeycloakIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(patient);
        _userRepository.GetRoleIdAsync(UserRoles.Patient, Arg.Any<CancellationToken>()).Returns(PatientRoleId);

        _nutritionistPatientRepository
            .FindActiveByPatientAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((NutritionistPatient?)null);

        var nutritionist = User.CreateNutritionist(
            Guid.NewGuid(), "kc-nutri", "n@cauce.local", "Nutri Demo", NutritionistRoleId);
        // La fábrica lo crea pendiente (acta A51); el escenario feliz necesita uno que ya pueda atender.
        nutritionist.Activate();
        var invitation = InvitationCode.Generate(
            Guid.NewGuid(), Code, nutritionist.Id, DateTime.UtcNow, InvitationCode.Validity);

        _invitationCodeRepository.FindByCodeAsync(Code, Arg.Any<CancellationToken>()).Returns(invitation);
        _userRepository.FindByIdAsync(nutritionist.Id, Arg.Any<CancellationToken>()).Returns(nutritionist);
        _assignmentService
            .EstablishAssignmentAsync(
                Arg.Any<Guid>(), Arg.Any<InvitationCode>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns((true, (Guid?)Guid.NewGuid()));

        return (patient, nutritionist, invitation);
    }

    /// <summary>
    /// Sustituye al nutricionista del escenario por uno en el estado indicado.
    /// </summary>
    /// <remarks>
    /// El dominio no ofrece una transición pública que deje a un nutricionista en
    /// <see cref="UserStatus.Inactive"/>: <c>Anonymize</c> es exclusivo de pacientes. Para ese estado se
    /// construye un doble que lo alcanza por la vía disponible, porque lo que el handler observa es
    /// únicamente <c>Status</c>.
    /// </remarks>
    private User ArrangeNutritionistWithStatus(UserStatus status, Guid nutritionistId)
    {
        User nutritionist;
        switch (status)
        {
            case UserStatus.Suspended:
                nutritionist = User.CreateNutritionist(
                    nutritionistId, "kc-nutri", "n@cauce.local", "Nutri Demo", NutritionistRoleId);
                nutritionist.Suspend();
                break;

            case UserStatus.Inactive:
                nutritionist = User.CreatePatient(
                    nutritionistId, "kc-nutri", "n@cauce.local", "Nutri Demo", PatientRoleId);
                nutritionist.Anonymize(PatientRoleId);
                break;

            default: // PendingActivation: el estado con el que nace un nutricionista (acta A51).
                nutritionist = User.CreateNutritionist(
                    nutritionistId, "kc-nutri", "n@cauce.local", "Nutri Demo", NutritionistRoleId);
                break;
        }

        nutritionist.Status.Should().Be(status, "el doble debe quedar en el estado que la prueba ejercita");
        _userRepository.FindByIdAsync(nutritionistId, Arg.Any<CancellationToken>()).Returns(nutritionist);
        return nutritionist;
    }

    // ----- Caso feliz -----

    [Fact]
    public async Task Handle_ActiveNutritionist_AssignsAndReturnsTheNutritionist()
    {
        var (_, nutritionist, _) = ArrangeHappyPath();

        var result = await Act(CreateHandler());

        result.NutritionistId.Should().Be(nutritionist.Id);
        result.NutritionistFullName.Should().Be("Nutri Demo");
        result.AssignedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Handle_ActiveNutritionist_ConsumesTheCodeAndEstablishesTheAssignment()
    {
        var (patient, _, invitation) = ArrangeHappyPath();

        await Act(CreateHandler());

        // Los dos pasos: sin el segundo el paciente quedaría con el código gastado y sin vínculo real.
        await _assignmentService.Received(1).ConsumeInvitationAsync(
            patient.Id, invitation, Arg.Any<DateTime>(), LinkContext.PostRegistrationLink, Arg.Any<CancellationToken>());
        await _assignmentService.Received(1)
            .EstablishAssignmentAsync(
                patient.Id, invitation, Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ActiveNutritionist_AuditsTheRedemptionAsAssigned()
    {
        ArrangeHappyPath();

        await Act(CreateHandler());

        await _auditLogger.Received(1).LogAsync(
            AuditActionType.NutritionistAssignment,
            nameof(InvitationCode),
            Arg.Any<Guid?>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Is<string?>(context => context != null && context.Contains("assigned")),
            Arg.Any<Guid?>(),
            Arg.Any<CancellationToken>());
    }

    // ----- Nutricionista no disponible (D11) -----

    [Theory]
    [InlineData(UserStatus.PendingActivation, "pending_activation")]
    [InlineData(UserStatus.Inactive, "inactive")]
    [InlineData(UserStatus.Suspended, "suspended")]
    public async Task Handle_NutritionistNotActive_ThrowsWithTheExactReason(UserStatus status, string expectedReason)
    {
        var (_, nutritionist, _) = ArrangeHappyPath();
        ArrangeNutritionistWithStatus(status, nutritionist.Id);

        var act = async () => await Act(CreateHandler());

        var thrown = await act.Should().ThrowAsync<NutritionistNotAvailableException>();
        thrown.Which.Status.Should().Be(status);
        thrown.Which.Reason.Should().Be(expectedReason);
    }

    [Theory]
    [InlineData(UserStatus.PendingActivation)]
    [InlineData(UserStatus.Inactive)]
    [InlineData(UserStatus.Suspended)]
    public async Task Handle_NutritionistNotActive_DoesNotConsumeTheCode(UserStatus status)
    {
        var (_, nutritionist, _) = ArrangeHappyPath();
        ArrangeNutritionistWithStatus(status, nutritionist.Id);

        var act = async () => await Act(CreateHandler());
        await act.Should().ThrowAsync<NutritionistNotAvailableException>();

        // El invariante de D11: el código queda intacto y puede reemitirse.
        await _assignmentService.DidNotReceive().ConsumeInvitationAsync(
            Arg.Any<Guid>(), Arg.Any<InvitationCode>(), Arg.Any<DateTime>(),
            Arg.Any<LinkContext>(), Arg.Any<CancellationToken>());
        await _assignmentService.DidNotReceive()
            .EstablishAssignmentAsync(
                Arg.Any<Guid>(), Arg.Any<InvitationCode>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(UserStatus.PendingActivation)]
    [InlineData(UserStatus.Inactive)]
    [InlineData(UserStatus.Suspended)]
    public async Task Handle_NutritionistNotActive_AuditsTheRejectionWithTheExactStatus(UserStatus status)
    {
        var (_, nutritionist, _) = ArrangeHappyPath();
        ArrangeNutritionistWithStatus(status, nutritionist.Id);

        var act = async () => await Act(CreateHandler());
        await act.Should().ThrowAsync<NutritionistNotAvailableException>();

        await _auditLogger.Received(1).LogAsync(
            AuditActionType.NutritionistAssignment,
            nameof(InvitationCode),
            Arg.Any<Guid?>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Is<string?>(context => context != null
                && context.Contains("rejected")
                && context.Contains(status.ToString())),
            Arg.Any<Guid?>(),
            Arg.Any<CancellationToken>());

        // La bitácora del rechazo debe persistir aunque la operación falle.
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    // ----- Resto de rechazos -----

    [Fact]
    public async Task Handle_PatientAlreadyAssigned_Throws()
    {
        ArrangeHappyPath();
        _nutritionistPatientRepository
            .FindActiveByPatientAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(NutritionistPatient.Establish(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow));

        var act = async () => await Act(CreateHandler());

        await act.Should().ThrowAsync<PatientAlreadyAssignedException>();
        await _assignmentService.DidNotReceive().ConsumeInvitationAsync(
            Arg.Any<Guid>(), Arg.Any<InvitationCode>(), Arg.Any<DateTime>(),
            Arg.Any<LinkContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnknownCode_ThrowsInvalidInvitationCode()
    {
        ArrangeHappyPath();
        _invitationCodeRepository.FindByCodeAsync(Code, Arg.Any<CancellationToken>())
            .Returns((InvitationCode?)null);

        var act = async () => await Act(CreateHandler());

        await act.Should().ThrowAsync<InvalidInvitationCodeException>();
    }

    [Fact]
    public async Task Handle_ExpiredCode_ThrowsExpiredInvitationCode()
    {
        var (_, nutritionist, _) = ArrangeHappyPath();
        var expired = InvitationCode.Generate(
            Guid.NewGuid(), Code, nutritionist.Id, DateTime.UtcNow.AddDays(-30), InvitationCode.Validity);
        _invitationCodeRepository.FindByCodeAsync(Code, Arg.Any<CancellationToken>()).Returns(expired);

        var act = async () => await Act(CreateHandler());

        await act.Should().ThrowAsync<ExpiredInvitationCodeException>();
    }

    [Fact]
    public async Task Handle_AlreadyUsedCode_ThrowsInvitationCodeAlreadyUsed()
    {
        var (_, nutritionist, _) = ArrangeHappyPath();
        var used = InvitationCode.Generate(
            Guid.NewGuid(), Code, nutritionist.Id, DateTime.UtcNow, InvitationCode.Validity);
        used.MarkAsUsed(Guid.NewGuid(), DateTime.UtcNow);
        _invitationCodeRepository.FindByCodeAsync(Code, Arg.Any<CancellationToken>()).Returns(used);

        var act = async () => await Act(CreateHandler());

        await act.Should().ThrowAsync<InvitationCodeAlreadyUsedException>();
    }

    [Fact]
    public async Task Handle_MissingNutritionistAccount_ThrowsInvalidInvitationCode()
    {
        var (_, nutritionist, _) = ArrangeHappyPath();
        _userRepository.FindByIdAsync(nutritionist.Id, Arg.Any<CancellationToken>()).Returns((User?)null);

        var act = async () => await Act(CreateHandler());

        // Inconsistencia de datos: no se filtra al cliente, se trata como código inválido.
        await act.Should().ThrowAsync<InvalidInvitationCodeException>();
    }

    [Fact]
    public async Task Handle_AuthenticatedUserIsNotAPatient_ThrowsUnauthorized()
    {
        ArrangeHappyPath();
        _userRepository.GetRoleIdAsync(UserRoles.Patient, Arg.Any<CancellationToken>()).Returns(NutritionistRoleId);

        var act = async () => await Act(CreateHandler());

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }
}
