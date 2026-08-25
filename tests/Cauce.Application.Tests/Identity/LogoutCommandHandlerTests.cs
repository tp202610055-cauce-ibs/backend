using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Identity.UseCases.Logout;
using FluentAssertions;
using NSubstitute;

namespace Cauce.Application.Tests.Identity;

/// <summary>
/// Pruebas del handler <see cref="LogoutCommandHandler"/>. El cierre de sesión es un passthrough: el
/// comportamiento best-effort ante un token ya inválido vive en <c>KeycloakTokenClient</c> y se prueba
/// allí.
/// </summary>
public sealed class LogoutCommandHandlerTests
{
    private readonly IKeycloakTokenClient _tokenClient = Substitute.For<IKeycloakTokenClient>();

    private LogoutCommandHandler CreateHandler() => new(_tokenClient);

    [Fact]
    public async Task Handle_ValidRefreshToken_RevokesItAgainstKeycloak()
    {
        await CreateHandler().Handle(new LogoutCommand("refresh-abc", "cauce-mobile"), CancellationToken.None);

        await _tokenClient.Received(1)
            .LogoutAsync("refresh-abc", "cauce-mobile", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PropagatesTheClientIdReceived()
    {
        await CreateHandler().Handle(new LogoutCommand("refresh-abc", "cauce-web-portal"), CancellationToken.None);

        await _tokenClient.Received(1)
            .LogoutAsync(Arg.Any<string>(), "cauce-web-portal", Arg.Any<CancellationToken>());
        await _tokenClient.DidNotReceive()
            .LogoutAsync(Arg.Any<string>(), "cauce-mobile", Arg.Any<CancellationToken>());
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
