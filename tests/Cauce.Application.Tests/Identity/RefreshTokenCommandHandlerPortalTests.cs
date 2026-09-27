using Cauce.Application.Common.Exceptions;
using Cauce.Application.Common.Identity;
using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Identity.UseCases.RefreshToken;
using Cauce.Domain.Auditing.Enums;
using Cauce.Domain.Identity;
using Cauce.Domain.Identity.Exceptions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Cauce.Application.Tests.Identity;

/// <summary>
/// Pruebas de <see cref="RefreshTokenCommandHandler"/> en el canal portal y de la auditoría de cada
/// rechazo con su causa (acta A68).
/// </summary>
public sealed class RefreshTokenCommandHandlerPortalTests
{
    private const string Subject = "kc-nutri";
    private const string RefreshToken = "refresh-portal";
    private const int PatientRoleId = 1;
    private const int NutritionistRoleId = 2;

    private readonly IKeycloakTokenClient _tokenClient = Substitute.For<IKeycloakTokenClient>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IAuditLogger _auditLogger = Substitute.For<IAuditLogger>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ILogger<RefreshTokenCommandHandler> _logger =
        Substitute.For<ILogger<RefreshTokenCommandHandler>>();

    /// <summary>
    /// El catálogo de roles de la prueba resuelve los dos roles del sistema.
    /// </summary>
    public RefreshTokenCommandHandlerPortalTests()
    {
        _userRepository.GetRoleNameAsync(PatientRoleId, Arg.Any<CancellationToken>()).Returns(UserRoles.Patient);
        _userRepository.GetRoleNameAsync(NutritionistRoleId, Arg.Any<CancellationToken>()).Returns(UserRoles.Nutritionist);
    }

    private RefreshTokenCommandHandler CreateHandler() =>
        new(_tokenClient, _userRepository, _auditLogger, _unitOfWork, _logger);

    private static RefreshTokenCommand PortalCommand(string refreshToken = RefreshToken) =>
        new(refreshToken, OidcClients.WebPortal, LoginChannel.Portal);

    private void GivenKeycloakRefreshes()
    {
        _tokenClient
            .RefreshAsync(RefreshToken, OidcClients.WebPortal, Arg.Any<CancellationToken>())
            .Returns(new KeycloakTokenResult("access", "rotado", 900, 1800, "Bearer", Subject));
    }

    private User GivenLocalUser(User user)
    {
        _userRepository.FindByKeycloakIdAsync(Subject, Arg.Any<CancellationToken>()).Returns(user);
        return user;
    }

    private static User ActiveNutritionist()
    {
        var user = User.CreateNutritionist(Guid.NewGuid(), Subject, "n@cauce.local", "Nutri", NutritionistRoleId);
        user.Activate();
        return user;
    }

    private Task AssertAuditedAsync(AuditActionType action, string channel, string? cause)
    {
        return _auditLogger.Received(1).LogAsync(
            action,
            nameof(User),
            Arg.Any<Guid?>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Is<string?>(context => context != null
                && context.Contains($"\"channel\":\"{channel}\"")
                && (cause == null ? !context.Contains("\"cause\"") : context.Contains($"\"cause\":\"{cause}\""))),
            Arg.Any<Guid?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PortalActiveNutritionist_ReturnsTokensAndAuditsPortalChannel()
    {
        GivenKeycloakRefreshes();
        GivenLocalUser(ActiveNutritionist());

        var result = await CreateHandler().Handle(PortalCommand(), CancellationToken.None);

        result.RefreshToken.Should().Be("rotado");
        await AssertAuditedAsync(AuditActionType.TokenRefresh, "portal", cause: null);
    }

    [Fact]
    public async Task Handle_PortalWithoutCookie_AuditsMissingCookieWithoutCallingKeycloak()
    {
        var act = async () => await CreateHandler().Handle(PortalCommand(string.Empty), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidRefreshTokenException>();
        await AssertAuditedAsync(AuditActionType.FailedTokenRefresh, "portal", AuthFailureCauses.MissingRefreshCookie);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _tokenClient.DidNotReceiveWithAnyArgs().RefreshAsync(default!, default!, default);
    }

    [Theory]
    [InlineData(OidcClients.WebPortal, LoginChannel.Mobile, "mobile")]
    [InlineData("cliente-inventado", LoginChannel.Mobile, "mobile")]
    [InlineData(OidcClients.Mobile, LoginChannel.Portal, "portal")]
    public async Task Handle_ClientNotOfTheChannel_AuditsAndThrowsUnsupportedClient(
        string clientId,
        LoginChannel channel,
        string channelName)
    {
        var act = async () => await CreateHandler()
            .Handle(new RefreshTokenCommand(RefreshToken, clientId, channel), CancellationToken.None);

        await act.Should().ThrowAsync<UnsupportedOidcClientException>();
        await AssertAuditedAsync(AuditActionType.FailedTokenRefresh, channelName, AuthFailureCauses.UnsupportedClient);
        await _tokenClient.DidNotReceiveWithAnyArgs().RefreshAsync(default!, default!, default);
    }

    [Fact]
    public async Task Handle_KeycloakRejectsToken_AuditsInvalidRefreshToken()
    {
        _tokenClient
            .RefreshAsync(RefreshToken, OidcClients.WebPortal, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidRefreshTokenException());

        var act = async () => await CreateHandler().Handle(PortalCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidRefreshTokenException>();
        await AssertAuditedAsync(AuditActionType.FailedTokenRefresh, "portal", AuthFailureCauses.InvalidRefreshToken);
    }

    [Fact]
    public async Task Handle_ClientMisconfigured_AuditsAndPropagates()
    {
        _tokenClient
            .RefreshAsync(RefreshToken, OidcClients.WebPortal, Arg.Any<CancellationToken>())
            .ThrowsAsync(new IdentityProviderMisconfiguredException(OidcClients.WebPortal, "unauthorized_client"));

        var act = async () => await CreateHandler().Handle(PortalCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<IdentityProviderMisconfiguredException>();
        await AssertAuditedAsync(AuditActionType.FailedTokenRefresh, "portal", AuthFailureCauses.ClientMisconfigured);
    }

    [Fact]
    public async Task Handle_PortalPatient_RevokesAuditsAndThrowsInvalidRefreshToken()
    {
        GivenKeycloakRefreshes();
        GivenLocalUser(User.CreatePatient(Guid.NewGuid(), Subject, "p@cauce.local", "Paciente", PatientRoleId, PatientCode.FromCorrelative(3)));

        var act = async () => await CreateHandler().Handle(PortalCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidRefreshTokenException>();
        await _tokenClient.Received(1).LogoutAsync("rotado", OidcClients.WebPortal, Arg.Any<CancellationToken>());
        await AssertAuditedAsync(AuditActionType.FailedTokenRefresh, "portal", AuthFailureCauses.RoleNotAllowed);
    }

    [Fact]
    public async Task Handle_PortalNutritionistSuspendedAfterLogin_LosesTheSession()
    {
        GivenKeycloakRefreshes();
        var user = ActiveNutritionist();
        user.Suspend();
        GivenLocalUser(user);

        var act = async () => await CreateHandler().Handle(PortalCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidRefreshTokenException>();
        await AssertAuditedAsync(AuditActionType.FailedTokenRefresh, "portal", AuthFailureCauses.AccountSuspended);
    }

    [Fact]
    public async Task Handle_MobilePatient_IsNotFilteredByRole()
    {
        _tokenClient
            .RefreshAsync(RefreshToken, OidcClients.Mobile, Arg.Any<CancellationToken>())
            .Returns(new KeycloakTokenResult("access", "rotado", 900, 2592000, "Bearer", Subject));
        GivenLocalUser(User.CreatePatient(Guid.NewGuid(), Subject, "p@cauce.local", "Paciente", PatientRoleId, PatientCode.FromCorrelative(4)));

        var result = await CreateHandler()
            .Handle(new RefreshTokenCommand(RefreshToken, OidcClients.Mobile), CancellationToken.None);

        result.User.Role.Should().Be(UserRoles.Patient);
        await AssertAuditedAsync(AuditActionType.TokenRefresh, "mobile", cause: null);
    }
}
