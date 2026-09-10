using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Common.Interfaces.Patients;
using Cauce.Application.Patients.Services;
using Cauce.Domain.Common;
using Cauce.Domain.Identity;
using Cauce.Domain.Identity.Enums;
using Cauce.Domain.Identity.Events;
using Cauce.Domain.Patients;
using FluentAssertions;
using NSubstitute;

namespace Cauce.Application.Tests.Patients.Services;

/// <summary>
/// Pruebas de <see cref="PatientNutritionistAssignmentService"/>, que reúne la vinculación
/// paciente-nutricionista extraída de los handlers de registro y de creación de perfil (acta A41).
/// </summary>
/// <remarks>
/// Las aserciones sobre <c>INutritionistPatientRepository</c> vienen migradas verbatim desde
/// <c>CreatePatientProfileCommandHandlerTests</c>, donde vivían antes del refactor (acta A45). Se
/// conservan los mismos stubs y las mismas verificaciones; lo único que cambia es el sujeto de la
/// prueba.
/// </remarks>
public sealed class PatientNutritionistAssignmentServiceTests
{
    private readonly IInvitationCodeRepository _invitationCodeRepository =
        Substitute.For<IInvitationCodeRepository>();
    private readonly INutritionistPatientRepository _nutritionistPatientRepository =
        Substitute.For<INutritionistPatientRepository>();
    private readonly IOutboxWriter _outboxWriter = Substitute.For<IOutboxWriter>();

    private PatientNutritionistAssignmentService CreateService() =>
        new(_invitationCodeRepository, _nutritionistPatientRepository, _outboxWriter);

    private static InvitationCode AnInvitation() =>
        InvitationCode.Generate(Guid.NewGuid(), "ABCDEFGH", Guid.NewGuid(), DateTime.UtcNow, InvitationCode.Validity);

    // ----- EstablishAssignmentAsync -----

    [Fact]
    public async Task EstablishAssignmentAsync_PatientWithoutInvitation_DoesNotAssign()
    {
        _invitationCodeRepository
            .FindByUsedByPatientIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((InvitationCode?)null);

        var (assigned, assignmentId) = await CreateService()
            .EstablishAssignmentAsync(Guid.NewGuid(), DateTime.UtcNow, CancellationToken.None);

        assigned.Should().BeFalse();
        assignmentId.Should().BeNull();
        await _nutritionistPatientRepository.DidNotReceive()
            .AddAsync(Arg.Any<NutritionistPatient>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EstablishAssignmentAsync_PatientUsedInvitation_CreatesTheAssignment()
    {
        _invitationCodeRepository
            .FindByUsedByPatientIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(AnInvitation());
        _nutritionistPatientRepository
            .ActiveAssignmentExistsAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var (assigned, assignmentId) = await CreateService()
            .EstablishAssignmentAsync(Guid.NewGuid(), DateTime.UtcNow, CancellationToken.None);

        assigned.Should().BeTrue();
        assignmentId.Should().NotBeNull();
        await _nutritionistPatientRepository.Received(1)
            .AddAsync(Arg.Any<NutritionistPatient>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EstablishAssignmentAsync_ActiveAssignmentAlreadyExists_DoesNotDuplicate()
    {
        // Caso que el handler nunca cubría: la operación es idempotente frente a una asignación viva.
        _invitationCodeRepository
            .FindByUsedByPatientIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(AnInvitation());
        _nutritionistPatientRepository
            .ActiveAssignmentExistsAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var (assigned, assignmentId) = await CreateService()
            .EstablishAssignmentAsync(Guid.NewGuid(), DateTime.UtcNow, CancellationToken.None);

        assigned.Should().BeFalse();
        assignmentId.Should().BeNull();
        await _nutritionistPatientRepository.DidNotReceive()
            .AddAsync(Arg.Any<NutritionistPatient>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EstablishAssignmentAsync_WithAResolvedCode_DoesNotQueryTheRepository()
    {
        // Sobrecarga para el canje post-registro: el MarkAsUsed todavía no se persistió, así que buscar
        // el código por paciente no lo encontraría y el vínculo no se crearía.
        _nutritionistPatientRepository
            .ActiveAssignmentExistsAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var (assigned, assignmentId) = await CreateService()
            .EstablishAssignmentAsync(Guid.NewGuid(), AnInvitation(), DateTime.UtcNow, CancellationToken.None);

        assigned.Should().BeTrue();
        assignmentId.Should().NotBeNull();
        await _invitationCodeRepository.DidNotReceive()
            .FindByUsedByPatientIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EstablishAssignmentAsync_WithAResolvedCode_StillRespectsAnActiveAssignment()
    {
        _nutritionistPatientRepository
            .ActiveAssignmentExistsAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(true);

        var (assigned, _) = await CreateService()
            .EstablishAssignmentAsync(Guid.NewGuid(), AnInvitation(), DateTime.UtcNow, CancellationToken.None);

        assigned.Should().BeFalse();
        await _nutritionistPatientRepository.DidNotReceive()
            .AddAsync(Arg.Any<NutritionistPatient>(), Arg.Any<CancellationToken>());
    }

    // ----- ConsumeInvitationAsync -----

    [Fact]
    public async Task ConsumeInvitationAsync_MarksTheCodeAsUsedByThePatient()
    {
        var invitation = AnInvitation();
        var patientId = Guid.NewGuid();
        var utcNow = DateTime.UtcNow;

        await CreateService().ConsumeInvitationAsync(patientId, invitation, utcNow, ct: CancellationToken.None);

        invitation.UsedByPatientId.Should().Be(patientId);
    }

    [Fact]
    public async Task ConsumeInvitationAsync_PublishesTheLinkEventThroughTheOutbox()
    {
        var invitation = AnInvitation();
        var patientId = Guid.NewGuid();

        await CreateService().ConsumeInvitationAsync(patientId, invitation, DateTime.UtcNow, ct: CancellationToken.None);

        await _outboxWriter.Received(1).PublishAsync(
            patientId,
            nameof(User),
            Arg.Is<IDomainEvent>(e => ((PatientLinkedToNutritionistEvent)e).NutritionistUserId == invitation.NutritionistId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConsumeInvitationAsync_WithoutContext_PublishesAsRegistrationLink()
    {
        var invitation = AnInvitation();

        await CreateService()
            .ConsumeInvitationAsync(Guid.NewGuid(), invitation, DateTime.UtcNow, ct: CancellationToken.None);

        // El registro omite el parámetro: el valor por defecto preserva su comportamiento (acta A43).
        await _outboxWriter.Received(1).PublishAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Is<IDomainEvent>(e =>
                ((PatientLinkedToNutritionistEvent)e).Context == LinkContext.RegistrationLink),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConsumeInvitationAsync_WithPostRegistrationContext_PublishesIt()
    {
        var invitation = AnInvitation();

        await CreateService().ConsumeInvitationAsync(
            Guid.NewGuid(),
            invitation,
            DateTime.UtcNow,
            LinkContext.PostRegistrationLink,
            CancellationToken.None);

        await _outboxWriter.Received(1).PublishAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Is<IDomainEvent>(e =>
                ((PatientLinkedToNutritionistEvent)e).Context == LinkContext.PostRegistrationLink),
            Arg.Any<CancellationToken>());
    }
}
