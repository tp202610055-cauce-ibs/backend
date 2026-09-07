using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Identity.UseCases.ResendVerificationEmail;
using Cauce.Domain.Auditing.Enums;
using Cauce.Domain.Identity;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Cauce.Application.Tests.Identity;

/// <summary>
/// Pruebas del handler de reenvío del correo de verificación (acta A40). Los tres desenlaces posibles
/// (cuenta inexistente, cuenta ya verificada y reenvío efectivo) terminan igual hacia afuera; lo que
/// cambia es si se invoca a Keycloak.
/// </summary>
public sealed class ResendVerificationEmailCommandHandlerTests
{
    private const string Email = "paciente@cauce.local";
    private const string KeycloakId = "kc-sub-1";
    private const int PatientRoleId = 1;

    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IKeycloakAdminClient _adminClient = Substitute.For<IKeycloakAdminClient>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ILogger<ResendVerificationEmailCommandHandler> _logger =
        Substitute.For<ILogger<ResendVerificationEmailCommandHandler>>();

    private ResendVerificationEmailCommandHandler CreateHandler() =>
        new(_userRepository, _adminClient, _unitOfWork, _logger);

    private User GivenLocalUser(bool emailVerified)
    {
        var user = User.CreatePatient(Guid.NewGuid(), KeycloakId, Email, "Paciente Demo", PatientRoleId);
        if (emailVerified)
        {
            user.VerifyEmail();
        }

        _userRepository.FindByEmailAsync(Email, Arg.Any<CancellationToken>()).Returns(user);
        return user;
    }

    [Fact]
    public async Task Handle_UserDoesNotExist_DoesNotCallKeycloak()
    {
        _userRepository.FindByEmailAsync(Email, Arg.Any<CancellationToken>()).Returns((User?)null);

        await CreateHandler().Handle(new ResendVerificationEmailCommand(Email), CancellationToken.None);

        await _adminClient.DidNotReceive().SendVerifyEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UserAlreadyVerified_DoesNotCallKeycloak()
    {
        GivenLocalUser(emailVerified: true);

        await CreateHandler().Handle(new ResendVerificationEmailCommand(Email), CancellationToken.None);

        // Reenviar a quien ya verificó no aporta nada, y distinguir el caso hacia afuera revelaría el
        // estado de la cuenta (decisión D4).
        await _adminClient.DidNotReceive().SendVerifyEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UserNotVerified_RequestsTheResendFromKeycloak()
    {
        GivenLocalUser(emailVerified: false);

        await CreateHandler().Handle(new ResendVerificationEmailCommand(Email), CancellationToken.None);

        await _adminClient.Received(1).SendVerifyEmailAsync(KeycloakId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_KeycloakFails_DoesNotPropagate()
    {
        GivenLocalUser(emailVerified: false);
        _adminClient
            .SendVerifyEmailAsync(KeycloakId, Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new HttpRequestException("Admin API caída"));

        var act = async () => await CreateHandler()
            .Handle(new ResendVerificationEmailCommand(Email), CancellationToken.None);

        // El envío es best-effort: la respuesta al cliente es uniforme por diseño.
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Handle_KeycloakCancelled_DoesNotSwallowCancellation()
    {
        GivenLocalUser(emailVerified: false);
        _adminClient
            .SendVerifyEmailAsync(KeycloakId, Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new OperationCanceledException());

        var act = async () => await CreateHandler()
            .Handle(new ResendVerificationEmailCommand(Email), CancellationToken.None);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Handle_AnyOutcome_CommitsSoTheAuditRowPersists(bool userExists)
    {
        if (userExists)
        {
            GivenLocalUser(emailVerified: false);
        }
        else
        {
            _userRepository.FindByEmailAsync(Email, Arg.Any<CancellationToken>()).Returns((User?)null);
        }

        await CreateHandler().Handle(new ResendVerificationEmailCommand(Email), CancellationToken.None);

        // El AuditingBehavior enrola la bitácora en el ChangeTracker pero no confirma: sin este
        // SaveChanges el intento no quedaría registrado en ningún lado.
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Command_DeclaresTheAuditContract()
    {
        var command = new ResendVerificationEmailCommand(Email);

        command.AuditActionType.Should().Be(AuditActionType.VerificationEmailResendRequest);
        command.AuditEntityType.Should().Be(nameof(User));
        // El contexto lleva el correo enmascarado, nunca la PII completa.
        command.AuditAdditionalContext.Should().NotBeNull().And.NotContain(Email);
    }
}
