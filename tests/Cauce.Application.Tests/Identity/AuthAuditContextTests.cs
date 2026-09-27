using System.Text.Json;
using Cauce.Application.Common.Identity;
using FluentAssertions;

namespace Cauce.Application.Tests.Identity;

/// <summary>
/// Pruebas de <see cref="AuthAuditContext"/>: la forma del contexto adicional de las filas de sesión que
/// escriben el middleware y el handler de renovación (acta A68).
/// </summary>
public sealed class AuthAuditContextTests
{
    [Fact]
    public void Build_WithCauseAndClient_WritesTheThreeKeys()
    {
        using var document = JsonDocument.Parse(
            AuthAuditContext.Build(LoginChannel.Portal, AuthFailureCauses.WrongPassword, OidcClients.WebPortal));

        document.RootElement.GetProperty("channel").GetString().Should().Be("portal");
        document.RootElement.GetProperty("cause").GetString().Should().Be("wrong_password");
        document.RootElement.GetProperty("clientId").GetString().Should().Be("cauce-web-portal");
    }

    [Fact]
    public void Build_SuccessfulAttempt_OmitsCauseAndClient()
    {
        using var document = JsonDocument.Parse(AuthAuditContext.Build(LoginChannel.Mobile, cause: null));

        document.RootElement.GetProperty("channel").GetString().Should().Be("mobile");
        document.RootElement.TryGetProperty("cause", out _).Should().BeFalse();
        document.RootElement.TryGetProperty("clientId", out _).Should().BeFalse();
    }
}
