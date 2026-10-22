using Cauce.Infrastructure.Identity;
using FluentAssertions;

namespace Cauce.Infrastructure.Tests.Identity;

/// <summary>
/// Pruebas de las URLs derivadas de <see cref="KeycloakOptions"/>: con la URL interna, el backend habla con
/// Keycloak por la red interna y el emisor sigue siendo la URL pública.
/// </summary>
public sealed class KeycloakOptionsTests
{
    private const string PublicAuthority = "https://portal.example.test/realms/cauce";

    private static KeycloakOptions Options(string internalBaseUrl = "") => new()
    {
        Authority = PublicAuthority,
        Realm = "cauce",
        Audience = "cauce-backend",
        ClientId = "cauce-backend",
        ClientSecret = "test-secret",
        InternalBaseUrl = internalBaseUrl
    };

    [Fact]
    public void WithoutInternalBaseUrl_EverythingDerivesFromTheAuthority()
    {
        var options = Options();

        options.BackchannelAuthority.Should().Be(PublicAuthority);
        options.ServerRoot.Should().Be("https://portal.example.test");
        options.MetadataAddress.Should().Be($"{PublicAuthority}/.well-known/openid-configuration");
    }

    [Theory]
    [InlineData("http://keycloak:8080")]
    [InlineData("http://keycloak:8080/")]
    public void WithInternalBaseUrl_BackchannelUsesItAndTheAuthorityStaysPublic(string internalBaseUrl)
    {
        var options = Options(internalBaseUrl);

        options.BackchannelAuthority.Should().Be("http://keycloak:8080/realms/cauce");
        options.ServerRoot.Should().Be("http://keycloak:8080");
        options.MetadataAddress.Should().Be("http://keycloak:8080/realms/cauce/.well-known/openid-configuration");
        options.Authority.Should().Be(PublicAuthority);
    }

    [Fact]
    public void WhitespaceInternalBaseUrl_IsIgnored()
    {
        Options("   ").BackchannelAuthority.Should().Be(PublicAuthority);
    }
}
