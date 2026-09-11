using Cauce.Application.Common.Exceptions;
using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Identity.UseCases.ResendNutritionistActivationEmail;
using Cauce.Domain.Auditing.Enums;
using Cauce.Domain.Identity;
using Cauce.Domain.Identity.Enums;
using Cauce.Domain.Identity.Exceptions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Cauce.Application.Tests.Identity;

/// <summary>
/// Pruebas del handler <see cref="ResendNutritionistActivationEmailCommandHandler"/> (acta A52): solo
/// reenvía a nutricionistas pendientes, y audita después de que Keycloak aceptó el envío.
/// </summary>
public sealed class ResendNutritionistActivationEmailCommandHandlerTests
{
    private const int PatientRoleId = 1;
    private const int NutritionistRoleId = 2;
    private const string KeycloakId = "kc-nutri";

    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IKeycloakAdminClient _keycloakAdminClient = Substitute.For<IKeycloakAdminClient>();
    private readonly IAuditLogger _auditLogger = Substitute.For<IAuditLogger>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ILogger<ResendNutritionistActivationEmailCommandHandler> _logger =
        Substitute.For<ILogger<ResendNutritionistActivationEmailCommandHandler>>();

    private ResendNutritionistActivationEmailCommandHandler CreateHandler()
    {
        _userRepository.GetRoleIdAsync(UserRoles.Nutritionist, Arg.Any<CancellationToken>()).Returns(NutritionistRoleId);
        return new ResendNutritionistActivationEmailCommandHandler(
            _userRepository, _keycloakAdminClient, _auditLogger, _unitOfWork, _logger);
    }

    private User GivenStoredUser(User user)
    {
        _userRepository.FindByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        return user;
    }

    private static User PendingNutritionist() =>
        User.CreateNutritionist(Guid.NewGuid(), KeycloakId, "n@cauce.local", "Nutri Demo", NutritionistRoleId);

    [Fact]
    public async Task Handle_PendingNutritionist_SendsTheLinkThenAuditsAndPersists()
    {
        var nutritionist = GivenStoredUser(PendingNutritionist());

        await CreateHandler().Handle(
            new ResendNutritionistActivationEmailCommand(nutritionist.Id), CancellationToken.None);

        // La bitácora registra reenvíos efectivos: la fila se escribe después de que Keycloak aceptó.
        Received.InOrder(() =>
        {
            _keycloakAdminClient.SendUpdatePasswordEmailAsync(KeycloakId, Arg.Any<CancellationToken>());
            _auditLogger.LogAsync(
                AuditActionType.ActivationEmailResend,
                Arg.Is(nameof(User)),
                nutritionist.Id,
                Arg.Is<string?>(hash => hash == null),
                Arg.Is<string?>(hash => hash == null),
                Arg.Any<string?>(),
                Arg.Any<Guid?>(),
                Arg.Any<CancellationToken>());
            _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task Handle_UnknownIdentifier_ThrowsNutritionistNotFound()
    {
        _userRepository.FindByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((User?)null);

        var act = () => CreateHandler().Handle(
            new ResendNutritionistActivationEmailCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NutritionistNotFoundException>();
        await _keycloakAdminClient.DidNotReceiveWithAnyArgs().SendUpdatePasswordEmailAsync(default!, default);
    }

    [Fact]
    public async Task Handle_PatientAccount_ThrowsNutritionistNotFound()
    {
        var patient = GivenStoredUser(
            User.CreatePatient(Guid.NewGuid(), "kc-patient", "p@cauce.local", "Paciente", PatientRoleId));

        var act = () => CreateHandler().Handle(
            new ResendNutritionistActivationEmailCommand(patient.Id), CancellationToken.None);

        // Una cuenta de otro rol responde igual que una inexistente.
        await act.Should().ThrowAsync<NutritionistNotFoundException>();
    }

    [Fact]
    public async Task Handle_ActiveNutritionist_ThrowsNotPendingActivationWithoutSending()
    {
        var nutritionist = PendingNutritionist();
        nutritionist.Activate();
        GivenStoredUser(nutritionist);

        var act = () => CreateHandler().Handle(
            new ResendNutritionistActivationEmailCommand(nutritionist.Id), CancellationToken.None);

        (await act.Should().ThrowAsync<NutritionistNotPendingActivationException>())
            .Which.Status.Should().Be(UserStatus.Active);
        await _keycloakAdminClient.DidNotReceiveWithAnyArgs().SendUpdatePasswordEmailAsync(default!, default);
    }

    [Fact]
    public async Task Handle_KeycloakFails_PropagatesWithoutPersisting()
    {
        var nutritionist = GivenStoredUser(PendingNutritionist());
        _keycloakAdminClient
            .SendUpdatePasswordEmailAsync(KeycloakId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new KeycloakIntegrationException("Keycloak no pudo enviar el correo."));

        var act = () => CreateHandler().Handle(
            new ResendNutritionistActivationEmailCommand(nutritionist.Id), CancellationToken.None);

        // El operador pidió el reenvío y tiene que saber que no salió: el error se propaga como 502.
        await act.Should().ThrowAsync<KeycloakIntegrationException>();
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Validator_EmptyIdentifier_IsInvalid()
    {
        var result = new ResendNutritionistActivationEmailCommandValidator()
            .Validate(new ResendNutritionistActivationEmailCommand(Guid.Empty));

        result.IsValid.Should().BeFalse();
    }
}
