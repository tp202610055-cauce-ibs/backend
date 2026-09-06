using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Identity.UseCases.Login;
using Cauce.Domain.Identity;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Cauce.Application.Tests.Identity;

/// <summary>
/// Pruebas de la sincronización de <c>emailVerified</c> desde Keycloak en <see cref="LoginCommandHandler"/>.
/// Keycloak es la fuente de verdad de la verificación, porque el enlace de confirmación lo emite y lo
/// procesa el realm sin pasar por el backend (acta A39). La sincronización es unidireccional: solo
/// promueve un correo a verificado, nunca lo revierte.
/// </summary>
public sealed class LoginCommandHandlerEmailSyncTests
{
    private const string Email = "p@cauce.local";
    private const string KeycloakId = "kc-sub-1";
    private const string Password = "Correct123!";
    private const string ClientId = "cauce-mobile";
    private const int PatientRoleId = 1;

    private readonly IKeycloakTokenClient _tokenClient = Substitute.For<IKeycloakTokenClient>();
    private readonly IKeycloakAdminClient _adminClient = Substitute.For<IKeycloakAdminClient>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ILogger<LoginCommandHandler> _logger = Substitute.For<ILogger<LoginCommandHandler>>();

    private LoginCommandHandler CreateHandler() =>
        new(_tokenClient, _adminClient, _userRepository, _unitOfWork, _logger);

    private static LoginCommand Command() => new(Email, Password, ClientId);

    private void GivenKeycloakAuthenticates()
    {
        _tokenClient
            .LoginAsync(Email, Password, ClientId, Arg.Any<CancellationToken>())
            .Returns(new KeycloakTokenResult("access", "refresh", 900, 2592000, "Bearer"));
    }

    private User GivenLocalUser(bool emailVerified)
    {
        var user = User.CreatePatient(Guid.NewGuid(), KeycloakId, Email, "Paciente Demo", PatientRoleId);
        if (emailVerified)
        {
            user.VerifyEmail();
        }

        _userRepository.FindByEmailAsync(Email, Arg.Any<CancellationToken>()).Returns(user);
        _userRepository.GetRoleNameAsync(PatientRoleId, Arg.Any<CancellationToken>()).Returns(UserRoles.Patient);
        return user;
    }

    [Fact]
    public async Task Handle_VerifiedInKeycloakButNotLocally_SyncsAndReturnsVerified()
    {
        GivenKeycloakAuthenticates();
        var user = GivenLocalUser(emailVerified: false);
        _adminClient.GetUserEmailVerifiedAsync(KeycloakId, Arg.Any<CancellationToken>()).Returns(true);

        var result = await CreateHandler().Handle(Command(), CancellationToken.None);

        user.EmailVerified.Should().BeTrue();
        result.User.EmailVerified.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_VerifiedInKeycloakButNotLocally_PersistsInTheSameTransaction()
    {
        GivenKeycloakAuthenticates();
        GivenLocalUser(emailVerified: false);
        _adminClient.GetUserEmailVerifiedAsync(KeycloakId, Arg.Any<CancellationToken>()).Returns(true);

        await CreateHandler().Handle(Command(), CancellationToken.None);

        // El sync no persiste por su cuenta: lo confirma el SaveChanges que ya hacía el handler para
        // la marca de último acceso, de modo que ambos cambios viajan en una sola transacción.
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_VerifiedInBothSides_LeavesStateUntouched()
    {
        GivenKeycloakAuthenticates();
        var user = GivenLocalUser(emailVerified: true);
        _adminClient.GetUserEmailVerifiedAsync(KeycloakId, Arg.Any<CancellationToken>()).Returns(true);

        var result = await CreateHandler().Handle(Command(), CancellationToken.None);

        user.EmailVerified.Should().BeTrue();
        result.User.EmailVerified.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_UnverifiedInBothSides_LeavesStateUntouched()
    {
        GivenKeycloakAuthenticates();
        var user = GivenLocalUser(emailVerified: false);
        _adminClient.GetUserEmailVerifiedAsync(KeycloakId, Arg.Any<CancellationToken>()).Returns(false);

        var result = await CreateHandler().Handle(Command(), CancellationToken.None);

        user.EmailVerified.Should().BeFalse();
        result.User.EmailVerified.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_UnverifiedInKeycloakButVerifiedLocally_DoesNotRevert()
    {
        GivenKeycloakAuthenticates();
        var user = GivenLocalUser(emailVerified: true);
        _adminClient.GetUserEmailVerifiedAsync(KeycloakId, Arg.Any<CancellationToken>()).Returns(false);

        var result = await CreateHandler().Handle(Command(), CancellationToken.None);

        // Des-verificar un correo es un cambio de estado sensible que exige un flujo explícito y
        // auditado; no puede ocurrir como efecto colateral de un inicio de sesión.
        user.EmailVerified.Should().BeTrue();
        result.User.EmailVerified.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_AdminApiFails_ContinuesWithLocalValue()
    {
        GivenKeycloakAuthenticates();
        var user = GivenLocalUser(emailVerified: false);
        _adminClient
            .GetUserEmailVerifiedAsync(KeycloakId, Arg.Any<CancellationToken>())
            .Returns<bool>(_ => throw new HttpRequestException("Admin API caída"));

        var result = await CreateHandler().Handle(Command(), CancellationToken.None);

        // El login no debe fallar por una sincronización auxiliar (acta A39, decisión D2).
        result.AccessToken.Should().Be("access");
        result.User.EmailVerified.Should().BeFalse();
        user.LastLoginAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_AdminApiCancelled_DoesNotSwallowCancellation()
    {
        GivenKeycloakAuthenticates();
        GivenLocalUser(emailVerified: false);
        _adminClient
            .GetUserEmailVerifiedAsync(KeycloakId, Arg.Any<CancellationToken>())
            .Returns<bool>(_ => throw new OperationCanceledException());

        var act = async () => await CreateHandler().Handle(Command(), CancellationToken.None);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
