using System.Net;
using System.Text;
using Cauce.Application.Common.Exceptions;
using Cauce.Infrastructure.Identity;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Cauce.Infrastructure.Tests.Identity;

/// <summary>
/// Pruebas de <see cref="KeycloakAdminClient"/> con un manejador HTTP falso que responde tanto a la
/// petición del token de service account como a la de la Admin API. Es el único nivel donde se puede
/// observar la ruta invocada y el manejo de códigos de error: ambos viven dentro del cliente y no
/// viajan por <c>IKeycloakAdminClient</c>, así que un doble de la interfaz no podría verificarlos.
/// </summary>
public sealed class KeycloakAdminClientTests
{
    private const string KeycloakId = "b8ebd09c-3bb3-4e7b-90dd-a55124bae0fd";

    [Fact]
    public async Task GetUserEmailVerifiedAsync_KeycloakReportsVerified_ReturnsTrue()
    {
        var client = CreateClient(UserResponse("""{"id":"x","emailVerified":true}"""));

        var verified = await client.GetUserEmailVerifiedAsync(KeycloakId);

        verified.Should().BeTrue();
    }

    [Fact]
    public async Task GetUserEmailVerifiedAsync_KeycloakReportsUnverified_ReturnsFalse()
    {
        var client = CreateClient(UserResponse("""{"id":"x","emailVerified":false}"""));

        var verified = await client.GetUserEmailVerifiedAsync(KeycloakId);

        verified.Should().BeFalse();
    }

    [Fact]
    public async Task GetUserEmailVerifiedAsync_PropertyMissing_ReturnsFalse()
    {
        // Defensivo: una representación de usuario sin el campo se trata como no verificado, que es el
        // valor conservador. No es un error de integración.
        var client = CreateClient(UserResponse("""{"id":"x"}"""));

        var verified = await client.GetUserEmailVerifiedAsync(KeycloakId);

        verified.Should().BeFalse();
    }

    [Fact]
    public async Task GetUserEmailVerifiedAsync_UserNotFound_ThrowsKeycloakIntegrationException()
    {
        var client = CreateClient(new HttpResponseMessage(HttpStatusCode.NotFound));

        var act = async () => await client.GetUserEmailVerifiedAsync(KeycloakId);

        // Un 404 no se normaliza a false: "no lo pude averiguar" y "no está verificado" son cosas
        // distintas, y confundirlas degradaría el estado local del usuario (acta A39).
        await act.Should().ThrowAsync<KeycloakIntegrationException>();
    }

    [Fact]
    public async Task GetUserEmailVerifiedAsync_ServerError_ThrowsKeycloakIntegrationException()
    {
        var client = CreateClient(new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var act = async () => await client.GetUserEmailVerifiedAsync(KeycloakId);

        await act.Should().ThrowAsync<KeycloakIntegrationException>();
    }

    [Fact]
    public async Task GetUserEmailVerifiedAsync_QueriesTheUserByIdentifier()
    {
        var handler = new RoutingHandler(UserResponse("""{"id":"x","emailVerified":true}"""));
        var client = CreateClient(handler);

        await client.GetUserEmailVerifiedAsync(KeycloakId);

        handler.LastAdminRequestUri!.AbsolutePath
            .Should().EndWith($"/admin/realms/cauce/users/{KeycloakId}");
    }

    [Fact]
    public async Task SendVerifyEmailAsync_KeycloakAccepts_DoesNotThrow()
    {
        var client = CreateClient(new HttpResponseMessage(HttpStatusCode.NoContent));

        var act = async () => await client.SendVerifyEmailAsync(KeycloakId);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SendVerifyEmailAsync_KeycloakFails_ThrowsKeycloakIntegrationException()
    {
        var client = CreateClient(new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var act = async () => await client.SendVerifyEmailAsync(KeycloakId);

        // El cliente propaga: quien decide si el fallo es tolerable es el llamador. En el reenvío lo es,
        // en el registro no.
        await act.Should().ThrowAsync<KeycloakIntegrationException>();
    }

    [Fact]
    public async Task SendVerifyEmailAsync_PostsToTheSendVerifyEmailAction()
    {
        var handler = new RoutingHandler(new HttpResponseMessage(HttpStatusCode.NoContent));
        var client = CreateClient(handler);

        await client.SendVerifyEmailAsync(KeycloakId);

        handler.LastAdminRequestUri!.AbsolutePath
            .Should().EndWith($"/admin/realms/cauce/users/{KeycloakId}/send-verify-email");
        handler.LastAdminRequestMethod.Should().Be(HttpMethod.Put);
    }

    private static KeycloakAdminClient CreateClient(HttpResponseMessage adminResponse) =>
        CreateClient(new RoutingHandler(adminResponse));

    private static KeycloakAdminClient CreateClient(RoutingHandler handler)
    {
        var httpClient = new HttpClient(handler);
        var options = Options.Create(new KeycloakOptions
        {
            Authority = "https://test.cauce.local/realms/cauce",
            Realm = "cauce",
            Audience = "cauce-backend",
            ClientId = "cauce-backend",
            ClientSecret = "test-secret"
        });

        return new KeycloakAdminClient(httpClient, options, NullLogger<KeycloakAdminClient>.Instance);
    }

    private static HttpResponseMessage UserResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    /// <summary>
    /// Manejador que distingue la petición del token de service account de la de la Admin API. El
    /// cliente pide el token por el mismo <see cref="HttpClient"/> antes de cada llamada, así que un
    /// manejador de respuesta única no alcanzaría.
    /// </summary>
    private sealed class RoutingHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _adminResponse;

        public RoutingHandler(HttpResponseMessage adminResponse)
        {
            _adminResponse = adminResponse;
        }

        public Uri? LastAdminRequestUri { get; private set; }

        public HttpMethod? LastAdminRequestMethod { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/protocol/openid-connect/token", StringComparison.Ordinal))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """{"access_token":"admin-token","expires_in":300}""",
                        Encoding.UTF8,
                        "application/json")
                });
            }

            LastAdminRequestUri = request.RequestUri;
            LastAdminRequestMethod = request.Method;
            return Task.FromResult(_adminResponse);
        }
    }
}
