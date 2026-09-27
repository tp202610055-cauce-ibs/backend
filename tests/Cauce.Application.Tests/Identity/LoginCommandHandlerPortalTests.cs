using Cauce.Application.Common.Exceptions;
using Cauce.Application.Common.Identity;
using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Identity.UseCases.Login;
using Cauce.Domain.Identity;
using Cauce.Domain.Identity.Exceptions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Cauce.Application.Tests.Identity;

/// <summary>
/// Pruebas del canal portal y de las causas internas de rechazo de <see cref="LoginCommandHandler"/>
/// (acta A68).
/// </summary>
public sealed class LoginCommandHandlerPortalTests
{
    private const string Email = "n@cauce.local";
    private const string Password = "Portal#2026";
    private const int PatientRoleId = 1;
    private const int NutritionistRoleId = 2;

    private readonly IKeycloakTokenClient _tokenClient = Substitute.For<IKeycloakTokenClient>();
    private readonly IKeycloakAdminClient _adminClient = Substitute.For<IKeycloakAdminClient>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly INutritionistActivationService _activationService = Substitute.For<INutritionistActivationService>();
    private readonly AuthenticationAttemptContext _attemptContext = new();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ILogger<LoginCommandHandler> _logger = Substitute.For<ILogger<LoginCommandHandler>>();

    /// <summary>
    /// Por defecto Keycloak informa al usuario habilitado y sin acciones pendientes, y el catálogo de
    /// roles resuelve los dos roles del sistema.
    /// </summary>
    public LoginCommandHandlerPortalTests()
    {
        _adminClient
            .GetUserStateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new KeycloakUserState(Enabled: true, RequiredActions: []));
        _userRepository.GetRoleNameAsync(PatientRoleId, Arg.Any<CancellationToken>()).Returns(UserRoles.Patient);
        _userRepository.GetRoleNameAsync(NutritionistRoleId, Arg.Any<CancellationToken>()).Returns(UserRoles.Nutritionist);
    }

    private LoginCommandHandler CreateHandler() =>
        new(_tokenClient, _adminClient, _userRepository, _activationService, _attemptContext, _unitOfWork, _logger);

    private static LoginCommand PortalCommand(string password = Password) =>
        new(Email, password, OidcClients.WebPortal, LoginChannel.Portal);

    private void GivenKeycloakAuthenticates(string clientId = OidcClients.WebPortal)
    {
        _tokenClient
            .LoginAsync(Email, Password, clientId, Arg.Any<CancellationToken>())
            .Returns(new KeycloakTokenResult("access", "refresh-portal", 900, 1800, "Bearer"));
    }

    private void GivenKeycloakRejects()
    {
        _tokenClient
            .LoginAsync(Email, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidCredentialsException());
    }

    private User GivenNutritionist(Action<User>? arrange = null)
    {
        var user = User.CreateNutritionist(Guid.NewGuid(), "kc-nutri", Email, "Nutri Demo", NutritionistRoleId);
        arrange?.Invoke(user);
        _userRepository.FindByEmailAsync(Email, Arg.Any<CancellationToken>()).Returns(user);
        return user;
    }

    [Fact]
    public async Task Handle_PortalActiveNutritionist_ReturnsTokensAndPersists()
    {
        GivenKeycloakAuthenticates();
        GivenNutritionist(user => user.Activate());

        var result = await CreateHandler().Handle(PortalCommand(), CancellationToken.None);

        result.AccessToken.Should().Be("access");
        result.User.Role.Should().Be(UserRoles.Nutritionist);
        _attemptContext.FailureCause.Should().BeNull();
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PortalPendingNutritionist_IsAllowedAndActivated()
    {
        // Su primer inicio de sesión es justamente el que la activa (acta A51): el portal no lo rechaza.
        GivenKeycloakAuthenticates();
        var user = GivenNutritionist();

        await CreateHandler().Handle(PortalCommand(), CancellationToken.None);

        await _activationService.Received(1)
            .ActivateIfPendingAsync(user, NutritionistActivationTrigger.Login, Arg.Any<CancellationToken>());
        _attemptContext.FailureCause.Should().BeNull();
    }

    [Fact]
    public async Task Handle_PortalPatient_RevokesTheSessionAndThrowsInvalidCredentials()
    {
        GivenKeycloakAuthenticates();
        var patient = User.CreatePatient(Guid.NewGuid(), "kc-pat", Email, "Paciente", PatientRoleId, PatientCode.FromCorrelative(7));
        _userRepository.FindByEmailAsync(Email, Arg.Any<CancellationToken>()).Returns(patient);

        var act = async () => await CreateHandler().Handle(PortalCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidCredentialsException>();
        _attemptContext.FailureCause.Should().Be(AuthFailureCauses.RoleNotAllowed);
        await _tokenClient.Received(1).LogoutAsync("refresh-portal", OidcClients.WebPortal, Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        patient.LastLoginAt.Should().BeNull();
    }

    [Fact]
    public async Task Handle_PortalSuspendedNutritionist_RevokesAndRecordsSuspended()
    {
        GivenKeycloakAuthenticates();
        GivenNutritionist(user =>
        {
            user.Activate();
            user.Suspend();
        });

        var act = async () => await CreateHandler().Handle(PortalCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidCredentialsException>();
        _attemptContext.FailureCause.Should().Be(AuthFailureCauses.AccountSuspended);
        await _tokenClient.Received(1).LogoutAsync(Arg.Any<string>(), OidcClients.WebPortal, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PortalInactiveNutritionist_RevokesAndRecordsInactive()
    {
        // No hay transición a Inactive para nutricionistas; la única forma de construir una cuenta
        // inactiva es anonimizarla con el mismo rol, que el catálogo de la prueba resuelve como
        // nutricionista.
        GivenKeycloakAuthenticates();
        var user = User.CreatePatient(Guid.NewGuid(), "kc-in", Email, "Inactiva", NutritionistRoleId, PatientCode.FromCorrelative(8));
        user.Anonymize(NutritionistRoleId);
        _userRepository.FindByEmailAsync(Email, Arg.Any<CancellationToken>()).Returns(user);

        var act = async () => await CreateHandler().Handle(PortalCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidCredentialsException>();
        _attemptContext.FailureCause.Should().Be(AuthFailureCauses.AccountInactive);
    }

    [Fact]
    public async Task Handle_PortalRejectionAndRevocationFails_StillThrowsInvalidCredentials()
    {
        GivenKeycloakAuthenticates();
        var patient = User.CreatePatient(Guid.NewGuid(), "kc-pat", Email, "Paciente", PatientRoleId, PatientCode.FromCorrelative(9));
        _userRepository.FindByEmailAsync(Email, Arg.Any<CancellationToken>()).Returns(patient);
        _tokenClient
            .LogoutAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Keycloak inalcanzable"));

        var act = async () => await CreateHandler().Handle(PortalCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidCredentialsException>();
    }

    [Fact]
    public async Task Handle_MobilePatient_IsNotFilteredByRole()
    {
        // Las reglas de rol son del portal: el móvil no cambia (acta A68).
        GivenKeycloakAuthenticates(OidcClients.Mobile);
        var patient = User.CreatePatient(Guid.NewGuid(), "kc-pat", Email, "Paciente", PatientRoleId, PatientCode.FromCorrelative(10));
        _userRepository.FindByEmailAsync(Email, Arg.Any<CancellationToken>()).Returns(patient);

        var result = await CreateHandler()
            .Handle(new LoginCommand(Email, Password, OidcClients.Mobile), CancellationToken.None);

        result.User.Role.Should().Be(UserRoles.Patient);
    }

    [Theory]
    [InlineData(OidcClients.WebPortal, LoginChannel.Mobile)]
    [InlineData("cliente-inventado", LoginChannel.Mobile)]
    [InlineData(OidcClients.Mobile, LoginChannel.Portal)]
    public async Task Handle_ClientNotOfTheChannel_ThrowsWithoutCallingKeycloak(string clientId, LoginChannel channel)
    {
        var act = async () => await CreateHandler()
            .Handle(new LoginCommand(Email, Password, clientId, channel), CancellationToken.None);

        await act.Should().ThrowAsync<UnsupportedOidcClientException>();
        _attemptContext.FailureCause.Should().Be(AuthFailureCauses.UnsupportedClient);
        await _tokenClient.DidNotReceiveWithAnyArgs().LoginAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task Handle_ClientMisconfigured_RecordsCauseAndPropagates()
    {
        _tokenClient
            .LoginAsync(Email, Password, OidcClients.WebPortal, Arg.Any<CancellationToken>())
            .ThrowsAsync(new IdentityProviderMisconfiguredException(OidcClients.WebPortal, "unauthorized_client"));

        var act = async () => await CreateHandler().Handle(PortalCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<IdentityProviderMisconfiguredException>();
        _attemptContext.FailureCause.Should().Be(AuthFailureCauses.ClientMisconfigured);
    }

    [Fact]
    public async Task Handle_AuthenticatedWithoutLocalAccount_RecordsLocalAccountMissing()
    {
        GivenKeycloakAuthenticates();
        _userRepository.FindByEmailAsync(Email, Arg.Any<CancellationToken>()).Returns((User?)null);

        var act = async () => await CreateHandler().Handle(PortalCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<UserLocalMissingException>();
        _attemptContext.FailureCause.Should().Be(AuthFailureCauses.LocalAccountMissing);
    }

    [Fact]
    public async Task Handle_RejectedUnknownEmail_RecordsUnknownAccount()
    {
        GivenKeycloakRejects();
        _userRepository.FindByEmailAsync(Email, Arg.Any<CancellationToken>()).Returns((User?)null);

        var act = async () => await CreateHandler().Handle(PortalCommand("Incorrecta#1"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidCredentialsException>();
        _attemptContext.FailureCause.Should().Be(AuthFailureCauses.UnknownAccount);
    }

    [Fact]
    public async Task Handle_RejectedAndLocked_ThrowsAccountLockedAndRecordsLocked()
    {
        GivenKeycloakRejects();
        var user = GivenNutritionist(nutritionist => nutritionist.Activate());
        var lockedUntil = DateTime.UtcNow.AddMinutes(1);
        _adminClient
            .GetBruteForceStatusAsync(user.KeycloakId, Arg.Any<CancellationToken>())
            .Returns(new BruteForceStatus(true, 5, null, null, lockedUntil));

        var act = async () => await CreateHandler().Handle(PortalCommand("Incorrecta#1"), CancellationToken.None);

        (await act.Should().ThrowAsync<AccountLockedException>()).Which.LockedUntil.Should().Be(lockedUntil);
        _attemptContext.FailureCause.Should().Be(AuthFailureCauses.AccountLocked);
    }

    [Fact]
    public async Task Handle_RejectedDisabledInKeycloak_RecordsDisabled()
    {
        GivenKeycloakRejects();
        var user = GivenNutritionist(nutritionist =>
        {
            nutritionist.Activate();
            nutritionist.Suspend();
        });
        _adminClient
            .GetUserStateAsync(user.KeycloakId, Arg.Any<CancellationToken>())
            .Returns(new KeycloakUserState(Enabled: false, RequiredActions: []));

        var act = async () => await CreateHandler().Handle(PortalCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidCredentialsException>();
        // Deshabilitada en Keycloak gana sobre el estado local: es lo que impide cualquier inicio de sesión.
        _attemptContext.FailureCause.Should().Be(AuthFailureCauses.AccountDisabled);
    }

    [Fact]
    public async Task Handle_RejectedPendingNutritionist_RecordsPendingActivation()
    {
        GivenKeycloakRejects();
        GivenNutritionist();

        var act = async () => await CreateHandler().Handle(PortalCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidCredentialsException>();
        _attemptContext.FailureCause.Should().Be(AuthFailureCauses.PendingActivation);
    }

    [Fact]
    public async Task Handle_RejectedSuspendedButEnabled_RecordsSuspended()
    {
        GivenKeycloakRejects();
        GivenNutritionist(nutritionist =>
        {
            nutritionist.Activate();
            nutritionist.Suspend();
        });

        var act = async () => await CreateHandler().Handle(PortalCommand("Incorrecta#1"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidCredentialsException>();
        _attemptContext.FailureCause.Should().Be(AuthFailureCauses.AccountSuspended);
    }

    [Fact]
    public async Task Handle_RejectedWithRequiredAction_RecordsRequiredActionPending()
    {
        GivenKeycloakRejects();
        var user = GivenNutritionist(nutritionist => nutritionist.Activate());
        _adminClient
            .GetUserStateAsync(user.KeycloakId, Arg.Any<CancellationToken>())
            .Returns(new KeycloakUserState(Enabled: true, RequiredActions: ["UPDATE_PASSWORD"]));

        var act = async () => await CreateHandler().Handle(PortalCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidCredentialsException>();
        _attemptContext.FailureCause.Should().Be(AuthFailureCauses.RequiredActionPending);
    }

    [Fact]
    public async Task Handle_RejectedActiveAccount_RecordsWrongPassword()
    {
        GivenKeycloakRejects();
        GivenNutritionist(nutritionist => nutritionist.Activate());

        var act = async () => await CreateHandler().Handle(PortalCommand("Incorrecta#1"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidCredentialsException>();
        _attemptContext.FailureCause.Should().Be(AuthFailureCauses.WrongPassword);
    }

    [Fact]
    public async Task Handle_RejectedAndAdminApiFails_RecordsUndetermined()
    {
        GivenKeycloakRejects();
        var user = GivenNutritionist(nutritionist => nutritionist.Activate());
        _adminClient
            .GetUserStateAsync(user.KeycloakId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Admin API caída"));

        var act = async () => await CreateHandler().Handle(PortalCommand("Incorrecta#1"), CancellationToken.None);

        // El 401 no cambia: un fallo de diagnóstico no se convierte en error del servidor.
        await act.Should().ThrowAsync<InvalidCredentialsException>();
        _attemptContext.FailureCause.Should().Be(AuthFailureCauses.Undetermined);
    }
}
