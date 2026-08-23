using Cauce.Application.Common.Identity;
using Cauce.Infrastructure.Email;
using Cauce.Infrastructure.Identity;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Cauce.Infrastructure.Tests.Identity;

/// <summary>
/// Pruebas de <see cref="ClientUrlProvider"/>. Verifican que el enlace de restablecimiento apunte al
/// destino correcto según el cliente OIDC de origen, y que un cliente desconocido no degrade en un
/// enlace silenciosamente inválido.
/// </summary>
public sealed class ClientUrlProviderTests
{
    private static ClientUrlProvider CreateProvider(
        string mobileBaseUrl = "cauce://",
        string portalBaseUrl = "http://localhost:5173")
    {
        var options = Options.Create(new EmailOptions
        {
            SmtpHost = "localhost",
            SmtpPort = 1025,
            FromAddress = "no-reply@cauce.local",
            FromName = "Cauce",
            MobileAppBaseUrl = mobileBaseUrl,
            PortalAppBaseUrl = portalBaseUrl
        });

        return new ClientUrlProvider(options);
    }

    [Fact]
    public void BuildPasswordResetLink_MobileClient_ReturnsDeepLink()
    {
        var link = CreateProvider().BuildPasswordResetLink("tok-123", OidcClients.Mobile);

        link.Should().StartWith("cauce://auth/password-reset");
        link.Should().Contain("token=tok-123");
        // El esquema no debe colapsar a una sola barra: "cauce:/auth/..." no resuelve como deep link.
        link.Should().NotStartWith("cauce:/auth");
    }

    [Fact]
    public void BuildPasswordResetLink_PortalClient_ReturnsHttpLink()
    {
        var link = CreateProvider().BuildPasswordResetLink("tok-123", OidcClients.WebPortal);

        link.Should().StartWith("http://localhost:5173/auth/password-reset");
        link.Should().Contain("token=tok-123");
    }

    [Fact]
    public void BuildPasswordResetLink_PortalBaseUrlWithTrailingSlash_DoesNotDuplicateIt()
    {
        var link = CreateProvider(portalBaseUrl: "http://localhost:5173/")
            .BuildPasswordResetLink("tok-123", OidcClients.WebPortal);

        link.Should().StartWith("http://localhost:5173/auth/password-reset");
        link.Should().NotContain("//auth/password-reset");
    }

    [Fact]
    public void BuildPasswordResetLink_UnknownClient_ThrowsArgumentException()
    {
        var act = () => CreateProvider().BuildPasswordResetLink("tok-123", "cliente-desconocido");

        act.Should().Throw<ArgumentException>().WithParameterName("clientId");
    }
}
