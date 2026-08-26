using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Identity.UseCases.Login;
using Cauce.Domain.Identity;
using Cauce.Domain.Identity.Exceptions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Cauce.Application.Tests.Identity;

/// <summary>
/// Pruebas del bloqueo por intentos fallidos en <see cref="LoginCommandHandler"/>. La fuente de verdad
/// del bloqueo es Keycloak; el handler solo traduce su estado a una respuesta que el cliente pueda
/// interpretar (US05 CA02).
/// </summary>
public sealed class LoginCommandHandlerLockoutTests
{
    private const string Email = "p@cauce.local";
    private const string KeycloakId = "kc-sub-1";

    private readonly IKeycloakTokenClient _tokenClient = Substitute.For<IKeycloakTokenClient>();
    private readonly IKeycloakAdminClient _adminClient = Substitute.For<IKeycloakAdminClient>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ILogger<LoginCommandHandler> _logger = Substitute.For<ILogger<LoginCommandHandler>>();

    private LoginCommandHandler CreateHandler() =>
        new(_tokenClient, _adminClient, _userRepository, _unitOfWork, _logger);

    private static LoginCommand Command() => new(Email, "wrong", "cauce-mobile");

    private void GivenKeycloakRejectsCredentials()
    {
        _tokenClient
            .LoginAsync(Email, "wrong", "cauce-mobile", Arg.Any<CancellationToken>())
            .Returns<KeycloakTokenResult>(_ => throw new InvalidCredentialsException());
    }

    private User GivenLocalUser()
    {
        var user = User.CreatePatient(Guid.NewGuid(), KeycloakId, Email, "Paciente", 1);
        _userRepository.FindByEmailAsync(Email, Arg.Any<CancellationToken>()).Returns(user);
        return user;
    }

    [Fact]
    public async Task Handle_LockedAccount_ThrowsAccountLockedExceptionWithLockedUntil()
    {
        GivenKeycloakRejectsCredentials();
        GivenLocalUser();
        var lockedUntil = DateTime.UtcNow.AddSeconds(60);
        _adminClient
            .GetBruteForceStatusAsync(KeycloakId, Arg.Any<CancellationToken>())
            .Returns(new BruteForceStatus(Disabled: true, NumFailures: 5, LastFailure: 1, LastIPFailure: "127.0.0.1", LockedUntil: lockedUntil));

        var act = async () => await CreateHandler().Handle(Command(), CancellationToken.None);

        var thrown = await act.Should().ThrowAsync<AccountLockedException>();
        thrown.Which.LockedUntil.Should().Be(lockedUntil);
    }

    [Fact]
    public async Task Handle_NotLockedYet_PropagatesInvalidCredentialsException()
    {
        GivenKeycloakRejectsCredentials();
        GivenLocalUser();
        _adminClient
            .GetBruteForceStatusAsync(KeycloakId, Arg.Any<CancellationToken>())
            .Returns(new BruteForceStatus(Disabled: false, NumFailures: 2, LastFailure: 1, LastIPFailure: "127.0.0.1", LockedUntil: DateTime.UtcNow));

        var act = async () => await CreateHandler().Handle(Command(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidCredentialsException>();
    }

    [Fact]
    public async Task Handle_UnknownEmail_PropagatesInvalidCredentialsWithoutQueryingKeycloak()
    {
        GivenKeycloakRejectsCredentials();
        _userRepository.FindByEmailAsync(Email, Arg.Any<CancellationToken>()).Returns((User?)null);

        var act = async () => await CreateHandler().Handle(Command(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidCredentialsException>();
        // Responder distinto para un correo no registrado revelaría qué cuentas existen.
        await _adminClient.DidNotReceive()
            .GetBruteForceStatusAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoBruteForceRecord_PropagatesInvalidCredentialsException()
    {
        GivenKeycloakRejectsCredentials();
        GivenLocalUser();
        _adminClient
            .GetBruteForceStatusAsync(KeycloakId, Arg.Any<CancellationToken>())
            .Returns((BruteForceStatus?)null);

        var act = async () => await CreateHandler().Handle(Command(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidCredentialsException>();
    }

    [Fact]
    public async Task Handle_AdminApiFails_DegradesToInvalidCredentialsException()
    {
        GivenKeycloakRejectsCredentials();
        GivenLocalUser();
        _adminClient
            .GetBruteForceStatusAsync(KeycloakId, Arg.Any<CancellationToken>())
            .Returns<BruteForceStatus?>(_ => throw new HttpRequestException("Admin API caída"));

        var act = async () => await CreateHandler().Handle(Command(), CancellationToken.None);

        // Un fallo de diagnóstico no debe convertir un login rechazado en un 500 ni filtrar al
        // cliente que la consulta falló.
        await act.Should().ThrowAsync<InvalidCredentialsException>();
    }

    [Fact]
    public async Task Handle_AdminApiCancelled_DoesNotSwallowCancellation()
    {
        GivenKeycloakRejectsCredentials();
        GivenLocalUser();
        _adminClient
            .GetBruteForceStatusAsync(KeycloakId, Arg.Any<CancellationToken>())
            .Returns<BruteForceStatus?>(_ => throw new OperationCanceledException());

        var act = async () => await CreateHandler().Handle(Command(), CancellationToken.None);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
