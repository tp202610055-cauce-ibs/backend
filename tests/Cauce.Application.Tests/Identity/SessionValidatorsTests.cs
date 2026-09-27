using Cauce.Application.Common.Identity;
using Cauce.Application.Identity.UseCases.Login;
using Cauce.Application.Identity.UseCases.Logout;
using Cauce.Application.Identity.UseCases.RefreshToken;
using FluentAssertions;

namespace Cauce.Application.Tests.Identity;

/// <summary>
/// Pruebas de los validadores de inicio, renovación y cierre de sesión, con una prueba por cada entrada
/// inválida. Que el cliente corresponda al canal no es regla de validador sino del handler, que la audita
/// (acta A68).
/// </summary>
public sealed class SessionValidatorsTests
{
    private const string Email = "n@cauce.local";
    private const string Password = "Portal#2026";

    private static bool IsValid(LoginCommand command) => new LoginCommandValidator().Validate(command).IsValid;

    private static bool IsValid(RefreshTokenCommand command) => new RefreshTokenCommandValidator().Validate(command).IsValid;

    private static bool IsValid(LogoutCommand command) => new LogoutCommandValidator().Validate(command).IsValid;

    [Theory]
    [InlineData(LoginChannel.Mobile, OidcClients.Mobile)]
    [InlineData(LoginChannel.Portal, OidcClients.WebPortal)]
    public void LoginValidator_ValidCommand_Succeeds(LoginChannel channel, string clientId)
    {
        IsValid(new LoginCommand(Email, Password, clientId, channel)).Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("no-es-un-correo")]
    public void LoginValidator_InvalidEmail_Fails(string email)
    {
        IsValid(new LoginCommand(email, Password, OidcClients.Mobile)).Should().BeFalse();
    }

    [Fact]
    public void LoginValidator_EmailLongerThan320_Fails()
    {
        var email = new string('a', 310) + "@cauce.local";

        IsValid(new LoginCommand(email, Password, OidcClients.Mobile)).Should().BeFalse();
    }

    [Fact]
    public void LoginValidator_EmptyPassword_Fails()
    {
        IsValid(new LoginCommand(Email, string.Empty, OidcClients.Mobile)).Should().BeFalse();
    }

    [Fact]
    public void LoginValidator_PasswordLongerThan200_Fails()
    {
        IsValid(new LoginCommand(Email, new string('x', 201), OidcClients.Mobile)).Should().BeFalse();
    }

    [Fact]
    public void LoginValidator_EmptyClientId_Fails()
    {
        IsValid(new LoginCommand(Email, Password, string.Empty)).Should().BeFalse();
    }

    [Fact]
    public void LoginValidator_ClientIdLongerThan100_Fails()
    {
        IsValid(new LoginCommand(Email, Password, new string('c', 101))).Should().BeFalse();
    }

    [Fact]
    public void LoginValidator_ChannelOutOfRange_Fails()
    {
        IsValid(new LoginCommand(Email, Password, OidcClients.Mobile, (LoginChannel)99)).Should().BeFalse();
    }

    [Fact]
    public void RefreshValidator_MobileWithoutToken_Fails()
    {
        IsValid(new RefreshTokenCommand(string.Empty, OidcClients.Mobile)).Should().BeFalse();
    }

    [Fact]
    public void RefreshValidator_PortalWithoutToken_Succeeds()
    {
        // La cookie ausente llega al handler, que la audita como missing_refresh_cookie.
        IsValid(new RefreshTokenCommand(string.Empty, OidcClients.WebPortal, LoginChannel.Portal)).Should().BeTrue();
    }

    [Fact]
    public void RefreshValidator_EmptyClientId_Fails()
    {
        IsValid(new RefreshTokenCommand("token", string.Empty)).Should().BeFalse();
    }

    [Fact]
    public void RefreshValidator_ClientIdLongerThan100_Fails()
    {
        IsValid(new RefreshTokenCommand("token", new string('c', 101))).Should().BeFalse();
    }

    [Fact]
    public void RefreshValidator_UnknownClient_PassesToTheHandler()
    {
        // Antes lo rechazaba el validador y el intento no quedaba auditado; ahora lo rechaza el handler.
        IsValid(new RefreshTokenCommand("token", "cliente-inventado")).Should().BeTrue();
    }

    [Fact]
    public void RefreshValidator_ChannelOutOfRange_Fails()
    {
        IsValid(new RefreshTokenCommand("token", OidcClients.Mobile, (LoginChannel)99)).Should().BeFalse();
    }

    [Fact]
    public void LogoutValidator_MobileWithoutToken_Fails()
    {
        IsValid(new LogoutCommand(string.Empty, OidcClients.Mobile)).Should().BeFalse();
    }

    [Fact]
    public void LogoutValidator_PortalWithoutToken_Succeeds()
    {
        IsValid(new LogoutCommand(string.Empty, OidcClients.WebPortal, LoginChannel.Portal)).Should().BeTrue();
    }

    [Fact]
    public void LogoutValidator_EmptyClientId_Fails()
    {
        IsValid(new LogoutCommand("token", string.Empty)).Should().BeFalse();
    }

    [Fact]
    public void LogoutValidator_ClientIdLongerThan100_Fails()
    {
        IsValid(new LogoutCommand("token", new string('c', 101))).Should().BeFalse();
    }

    [Fact]
    public void LogoutValidator_ChannelOutOfRange_Fails()
    {
        IsValid(new LogoutCommand("token", OidcClients.Mobile, (LoginChannel)99)).Should().BeFalse();
    }
}
