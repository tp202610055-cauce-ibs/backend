using Cauce.Application.Common.Exceptions;
using Cauce.Application.Common.Identity;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Identity.UseCases.Logout;
using FluentAssertions;
using NSubstitute;

namespace Cauce.Application.Tests.Identity;

/// <summary>
/// Pruebas del handler <see cref="LogoutCommandHandler"/>. El cierre de sesión es un passthrough: el
/// comportamiento best-effort ante un token ya inválido vive en <c>KeycloakTokenClient</c> y se prueba
/// allí. Lo propio del handler es que cada canal acepte solo su cliente OIDC (acta A68).
/// </summary>
public sealed class LogoutCommandHandlerTests
{
    private readonly IKeycloakTokenClient _tokenClient = Substitute.For<IKeycloakTokenClient>();
    private readonly AuthenticationAttemptContext _attemptContext = new();

    private LogoutCommandHandler CreateHandler() => new(_tokenClient, _attemptContext);

    [Fact]
    public async Task Handle_ValidRefreshToken_RevokesItAgainstKeycloak()
    {
        await CreateHandler().Handle(new LogoutCommand("refresh-abc", "cauce-mobile"), CancellationToken.None);

        await _tokenClient.Received(1)
            .LogoutAsync("refresh-abc", "cauce-mobile", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PortalChannel_RevokesWithThePortalClient()
    {
        await CreateHandler().Handle(
            new LogoutCommand("refresh-abc", OidcClients.WebPortal, LoginChannel.Portal),
            CancellationToken.None);

        await _tokenClient.Received(1)
            .LogoutAsync("refresh-abc", OidcClients.WebPortal, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(OidcClients.WebPortal, LoginChannel.Mobile)]
    [InlineData("cliente-inventado", LoginChannel.Mobile)]
    [InlineData(OidcClients.Mobile, LoginChannel.Portal)]
    public async Task Handle_ClientNotOfTheChannel_ThrowsAndRecordsUnsupportedClient(string clientId, LoginChannel channel)
    {
        var act = async () => await CreateHandler()
            .Handle(new LogoutCommand("refresh-abc", clientId, channel), CancellationToken.None);

        await act.Should().ThrowAsync<UnsupportedOidcClientException>();
        _attemptContext.FailureCause.Should().Be(AuthFailureCauses.UnsupportedClient);
        await _tokenClient.DidNotReceiveWithAnyArgs().LogoutAsync(default!, default!, default);
    }

    [Fact]
    public async Task Handle_PortalWithoutCookie_CompletesWithoutCallingKeycloak()
    {
        await CreateHandler().Handle(
            new LogoutCommand(string.Empty, OidcClients.WebPortal, LoginChannel.Portal),
            CancellationToken.None);

        await _tokenClient.DidNotReceiveWithAnyArgs().LogoutAsync(default!, default!, default);
        _attemptContext.FailureCause.Should().BeNull();
    }

    [Fact]
    public async Task Handle_KeycloakThrows_PropagatesTheFailure()
    {
        _tokenClient
            .LogoutAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new InvalidOperationException("Keycloak inalcanzable"));

        var act = async () => await CreateHandler()
            .Handle(new LogoutCommand("refresh-abc", "cauce-mobile"), CancellationToken.None);

        // El handler no enmascara fallos de infraestructura: el best-effort aplica solo a que Keycloak
        // rechace la revocación, no a que sea inalcanzable.
        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
