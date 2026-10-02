using Cauce.Application.Common.Exceptions;
using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Identity.UseCases.CreateNutritionist;
using Cauce.Domain.Identity;
using Cauce.Domain.Identity.Enums;
using Cauce.Domain.Identity.Exceptions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Cauce.Application.Tests.Identity;

/// <summary>
/// Pruebas del handler <see cref="CreateNutritionistCommandHandler"/>.
/// </summary>
public sealed class CreateNutritionistCommandHandlerTests
{
    private const int NutritionistRoleId = 2;
    private const string KeycloakId = "kc-nutri";

    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IKeycloakAdminClient _keycloakAdminClient = Substitute.For<IKeycloakAdminClient>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IAuditLogger _auditLogger = Substitute.For<IAuditLogger>();
    private readonly ILogger<CreateNutritionistCommandHandler> _logger =
        Substitute.For<ILogger<CreateNutritionistCommandHandler>>();

    private CreateNutritionistCommandHandler CreateHandler() => new(
        _userRepository,
        _keycloakAdminClient,
        _unitOfWork,
        _auditLogger,
        _logger);

    private static CreateNutritionistCommand Command() => new("n@cauce.local", "Nutri Uno");

    private void GivenKeycloakCreatesTheUser()
    {
        _userRepository.ExistsByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        _userRepository.GetRoleIdAsync(UserRoles.Nutritionist, Arg.Any<CancellationToken>()).Returns(NutritionistRoleId);
        _keycloakAdminClient
            .CreateUserAsync(Arg.Any<string>(), Arg.Any<string>(), UserRoles.Nutritionist, false, Arg.Any<CancellationToken>())
            .Returns(KeycloakId);
    }

    [Fact]
    public async Task Handle_ValidRequest_PersistsAPendingNutritionistWithoutPassword()
    {
        GivenKeycloakCreatesTheUser();

        var result = await CreateHandler().Handle(Command(), CancellationToken.None);

        result.Email.Should().Be("n@cauce.local");
        result.Status.Should().Be(UserStatus.PendingActivation);
        await _userRepository.Received(1).AddAsync(
            Arg.Is<User>(user => user.Status == UserStatus.PendingActivation && user.KeycloakId == KeycloakId),
            Arg.Any<CancellationToken>());
        // Ninguna contraseña: la define el propio nutricionista con el enlace de Keycloak (acta A52).
        await _keycloakAdminClient.DidNotReceiveWithAnyArgs().ResetPasswordAsync(default!, default!, default);
    }

    [Fact]
    public async Task Handle_ValidRequest_RequestsTheActivationLinkAfterCommitting()
    {
        GivenKeycloakCreatesTheUser();

        var result = await CreateHandler().Handle(Command(), CancellationToken.None);

        result.ActivationEmailSent.Should().BeTrue();
        Received.InOrder(() =>
        {
            _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>());
            _keycloakAdminClient.SendUpdatePasswordEmailAsync(KeycloakId, Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task Handle_ActivationLinkFails_ReturnsFlagFalseWithoutCompensating()
    {
        GivenKeycloakCreatesTheUser();
        _keycloakAdminClient
            .SendUpdatePasswordEmailAsync(KeycloakId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new KeycloakIntegrationException("Keycloak no pudo enviar el correo."));

        var result = await CreateHandler().Handle(Command(), CancellationToken.None);

        result.ActivationEmailSent.Should().BeFalse();
        // La cuenta ya está confirmada: borrarla en Keycloak dejaría la fila local huérfana.
        await _keycloakAdminClient.DidNotReceiveWithAnyArgs().DeleteUserAsync(default!, default);
    }

    [Fact]
    public async Task Handle_DuplicateEmail_ThrowsDuplicateEmailException()
    {
        _userRepository.ExistsByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);

        var act = () => CreateHandler().Handle(new CreateNutritionistCommand("n@cauce.local", "Nutri"), CancellationToken.None);

        await act.Should().ThrowAsync<DuplicateEmailException>();
    }

    [Fact]
    public async Task Handle_PersistenceFails_CompensatesByDeletingKeycloakUser()
    {
        GivenKeycloakCreatesTheUser();
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("db down"));

        var act = () => CreateHandler().Handle(new CreateNutritionistCommand("n@cauce.local", "Nutri"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        await _keycloakAdminClient.Received(1).DeleteUserAsync(KeycloakId, Arg.Any<CancellationToken>());
        // Sin cuenta confirmada no se pide ningún enlace.
        await _keycloakAdminClient.DidNotReceiveWithAnyArgs().SendUpdatePasswordEmailAsync(default!, default);
    }

    [Fact]
    public async Task Handle_RequestCancelledAfterKeycloakCreation_CompensatesWithoutInheritingTheCancellation()
    {
        GivenKeycloakCreatesTheUser();
        using var request = new CancellationTokenSource();
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            // El cliente corta la conexión justo cuando se confirma la transacción.
            request.Cancel();
            return Task.FromException<int>(new OperationCanceledException(request.Token));
        });
        (bool Cancelled, bool Cancellable)? compensationToken = null;
        _keycloakAdminClient
            .When(client => client.DeleteUserAsync(KeycloakId, Arg.Any<CancellationToken>()))
            .Do(call =>
            {
                var token = call.Arg<CancellationToken>();
                compensationToken = (token.IsCancellationRequested, token.CanBeCanceled);
            });

        var act = () => CreateHandler().Handle(Command(), request.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        compensationToken.Should().Be((false, true), "la compensación sale igual y con su propio tope de tiempo (acta A70)");
    }
}
