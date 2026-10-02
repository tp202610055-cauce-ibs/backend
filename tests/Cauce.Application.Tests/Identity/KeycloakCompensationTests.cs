using System.Diagnostics;
using Cauce.Application.Common.Identity;
using Cauce.Application.Common.Interfaces.Identity;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Cauce.Application.Tests.Identity;

/// <summary>
/// Pruebas de <see cref="KeycloakCompensation"/>, el borrado que deshace un alta a medias en Keycloak
/// (acta A70).
/// </summary>
public sealed class KeycloakCompensationTests
{
    private const string KeycloakId = "kc-id";

    private readonly IKeycloakAdminClient _keycloakAdminClient = Substitute.For<IKeycloakAdminClient>();
    private readonly ILogger _logger = Substitute.For<ILogger>();

    [Fact]
    public async Task TryDeleteUserAsync_KeycloakConfirms_ReturnsTrue()
    {
        var deleted = await KeycloakCompensation.TryDeleteUserAsync(_keycloakAdminClient, KeycloakId, _logger);

        deleted.Should().BeTrue();
        await _keycloakAdminClient.Received(1).DeleteUserAsync(KeycloakId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TryDeleteUserAsync_KeycloakFails_ReturnsFalseWithoutThrowing()
    {
        // La excepción original es la que tiene que llegar al cliente: la compensación fallida solo se
        // registra para la limpieza manual.
        _keycloakAdminClient.DeleteUserAsync(KeycloakId, Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("keycloak down"));

        var deleted = await KeycloakCompensation.TryDeleteUserAsync(_keycloakAdminClient, KeycloakId, _logger);

        deleted.Should().BeFalse();
    }

    [Fact]
    public async Task TryDeleteUserAsync_KeycloakHangs_GivesUpAtItsOwnTimeout()
    {
        _keycloakAdminClient.DeleteUserAsync(KeycloakId, Arg.Any<CancellationToken>())
            .Returns(call => Task.Delay(Timeout.Infinite, call.Arg<CancellationToken>()));
        var stopwatch = Stopwatch.StartNew();

        var deleted = await KeycloakCompensation.TryDeleteUserAsync(
            _keycloakAdminClient, KeycloakId, _logger, TimeSpan.FromMilliseconds(100));

        deleted.Should().BeFalse();
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5), "el tope de tiempo es propio y no espera a Keycloak");
    }
}
