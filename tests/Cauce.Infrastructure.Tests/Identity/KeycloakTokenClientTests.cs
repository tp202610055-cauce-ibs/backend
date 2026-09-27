using System.Net;
using System.Text;
using Cauce.Application.Common.Exceptions;
using Cauce.Domain.Identity.Exceptions;
using Cauce.Infrastructure.Identity;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Cauce.Infrastructure.Tests.Identity;

/// <summary>
/// Pruebas de <see cref="KeycloakTokenClient"/> con un manejador HTTP falso que intercepta la
/// petición al endpoint de tokens. Es el único nivel donde se puede observar el <c>scope</c>
/// solicitado: la decisión vive dentro del cliente y no viaja por <c>IKeycloakTokenClient</c>, así que
/// un doble de la interfaz no podría verificarla.
/// </summary>
public sealed class KeycloakTokenClientTests
{
    private const string Subject = "b8ebd09c-3bb3-4e7b-90dd-a55124bae0fd";

    private const string PortalSecret = "portal-secret";

    [Fact]
    public async Task LoginAsync_MobileClient_RequestsOfflineAccessScope()
    {
        var handler = new CapturingHandler(TokenResponse());
        var client = CreateClient(handler);

        await client.LoginAsync("p@cauce.local", "Correct123!", "cauce-mobile");

        handler.LastForm.Should().ContainKey("scope");
        handler.LastForm["scope"].Should().Be("openid offline_access");
        handler.LastForm["grant_type"].Should().Be("password");
    }

    [Fact]
    public async Task LoginAsync_WebPortalClient_RequestsOnlyOpenIdScope()
    {
        var handler = new CapturingHandler(TokenResponse());
        var client = CreateClient(handler);

        await client.LoginAsync("n@cauce.local", "Correct123!", "cauce-web-portal");

        handler.LastForm["scope"].Should().Be("openid");
    }

    [Fact]
    public async Task LoginAsync_ExtractsSubjectFromAccessToken()
    {
        var handler = new CapturingHandler(TokenResponse());
        var client = CreateClient(handler);

        var result = await client.LoginAsync("p@cauce.local", "Correct123!", "cauce-mobile");

        result.Subject.Should().Be(Subject);
    }

    [Fact]
    public async Task RefreshAsync_SendsRefreshTokenGrant()
    {
        var handler = new CapturingHandler(TokenResponse());
        var client = CreateClient(handler);

        var result = await client.RefreshAsync("refresh-abc", "cauce-mobile");

        handler.LastForm["grant_type"].Should().Be("refresh_token");
        handler.LastForm["refresh_token"].Should().Be("refresh-abc");
        handler.LastForm["client_id"].Should().Be("cauce-mobile");
        result.Subject.Should().Be(Subject);
    }

    [Fact]
    public async Task RefreshAsync_InvalidGrant_ThrowsInvalidRefreshTokenException()
    {
        var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(
                """{"error":"invalid_grant","error_description":"Token is not active"}""",
                Encoding.UTF8,
                "application/json")
        };
        var client = CreateClient(new CapturingHandler(response));

        var act = async () => await client.RefreshAsync("vencido", "cauce-mobile");

        await act.Should().ThrowAsync<InvalidRefreshTokenException>();
    }

    [Fact]
    public async Task RefreshAsync_UnexpectedStatus_ThrowsInvalidOperationException()
    {
        var response = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        var client = CreateClient(new CapturingHandler(response));

        var act = async () => await client.RefreshAsync("refresh-abc", "cauce-mobile");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task LogoutAsync_KeycloakRejectsRevocation_DoesNotThrow()
    {
        // El cierre de sesión es best-effort: si el refresh token ya venció, Keycloak responde 400 y
        // el cliente no debe convertir eso en un error para el usuario, que igual quedó desconectado.
        var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("""{"error":"invalid_grant"}""", Encoding.UTF8, "application/json")
        };
        var handler = new CapturingHandler(response);
        var client = CreateClient(handler);

        var act = async () => await client.LogoutAsync("token-vencido", "cauce-mobile");

        await act.Should().NotThrowAsync();
        handler.LastForm["refresh_token"].Should().Be("token-vencido");
    }

    [Fact]
    public async Task LoginAsync_WebPortalClient_SendsItsClientSecret()
    {
        var handler = new CapturingHandler(TokenResponse());
        var client = CreateClient(handler);

        await client.LoginAsync("n@cauce.local", "Correct123!", "cauce-web-portal");

        // El cliente del portal es confidencial: el secret viaja desde el backend y nunca desde el
        // navegador (acta A68).
        handler.LastForm["client_id"].Should().Be("cauce-web-portal");
        handler.LastForm["client_secret"].Should().Be(PortalSecret);
    }

    [Fact]
    public async Task LoginAsync_MobileClient_DoesNotSendAnySecret()
    {
        var handler = new CapturingHandler(TokenResponse());
        var client = CreateClient(handler);

        await client.LoginAsync("p@cauce.local", "Correct123!", "cauce-mobile");

        handler.LastForm.Should().NotContainKey("client_secret");
    }

    [Fact]
    public async Task LoginAsync_WebPortalWithoutSecret_ThrowsMisconfiguredWithoutCallingKeycloak()
    {
        var handler = new CapturingHandler(TokenResponse());
        var client = CreateClient(handler, webPortalSecret: string.Empty);

        var act = async () => await client.LoginAsync("n@cauce.local", "Correct123!", "cauce-web-portal");

        (await act.Should().ThrowAsync<IdentityProviderMisconfiguredException>())
            .Which.OAuthError.Should().Be("missing_client_secret");
        handler.CallCount.Should().Be(0);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "unauthorized_client")]
    [InlineData(HttpStatusCode.BadRequest, "unauthorized_client")]
    [InlineData(HttpStatusCode.Unauthorized, "invalid_client")]
    public async Task LoginAsync_ClientRejected_ThrowsMisconfiguredInsteadOfInvalidCredentials(
        HttpStatusCode status,
        string error)
    {
        var client = CreateClient(new CapturingHandler(ErrorResponse(status, error)));

        var act = async () => await client.LoginAsync("n@cauce.local", "Correct123!", "cauce-web-portal");

        (await act.Should().ThrowAsync<IdentityProviderMisconfiguredException>())
            .Which.OAuthError.Should().Be(error);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task LoginAsync_InvalidGrant_ThrowsInvalidCredentials(HttpStatusCode status)
    {
        var client = CreateClient(new CapturingHandler(ErrorResponse(status, "invalid_grant")));

        var act = async () => await client.LoginAsync("p@cauce.local", "Incorrecta1", "cauce-mobile");

        await act.Should().ThrowAsync<InvalidCredentialsException>();
    }

    [Fact]
    public async Task LoginAsync_ErrorBodyNotJson_ThrowsInvalidCredentials()
    {
        var response = new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("no es json", Encoding.UTF8, "text/plain")
        };
        var client = CreateClient(new CapturingHandler(response));

        var act = async () => await client.LoginAsync("p@cauce.local", "Incorrecta1", "cauce-mobile");

        await act.Should().ThrowAsync<InvalidCredentialsException>();
    }

    [Fact]
    public async Task RefreshAsync_WebPortalClient_SendsItsClientSecret()
    {
        var handler = new CapturingHandler(TokenResponse());
        var client = CreateClient(handler);

        await client.RefreshAsync("refresh-abc", "cauce-web-portal");

        handler.LastForm["client_secret"].Should().Be(PortalSecret);
    }

    [Fact]
    public async Task RefreshAsync_ClientRejected_ThrowsMisconfiguredInsteadOfInvalidRefreshToken()
    {
        var client = CreateClient(new CapturingHandler(ErrorResponse(HttpStatusCode.Unauthorized, "unauthorized_client")));

        var act = async () => await client.RefreshAsync("refresh-abc", "cauce-web-portal");

        await act.Should().ThrowAsync<IdentityProviderMisconfiguredException>();
    }

    [Fact]
    public async Task LogoutAsync_WebPortalClient_SendsItsClientSecret()
    {
        var handler = new CapturingHandler(new HttpResponseMessage(HttpStatusCode.NoContent));
        var client = CreateClient(handler);

        await client.LogoutAsync("refresh-abc", "cauce-web-portal");

        handler.LastForm["client_secret"].Should().Be(PortalSecret);
    }

    [Fact]
    public async Task LogoutAsync_ClientRejected_DoesNotThrow()
    {
        var client = CreateClient(new CapturingHandler(ErrorResponse(HttpStatusCode.Unauthorized, "unauthorized_client")));

        var act = async () => await client.LogoutAsync("refresh-abc", "cauce-web-portal");

        // Sigue siendo best-effort para el usuario; el error de configuración queda en el log.
        await act.Should().NotThrowAsync();
    }

    private static KeycloakTokenClient CreateClient(CapturingHandler handler, string webPortalSecret = PortalSecret)
    {
        var httpClient = new HttpClient(handler);
        var options = Options.Create(new KeycloakOptions
        {
            Authority = "https://test.cauce.local/realms/cauce",
            Realm = "cauce",
            Audience = "cauce-backend",
            ClientId = "cauce-backend",
            ClientSecret = "test-secret",
            WebPortalClientSecret = webPortalSecret
        });

        return new KeycloakTokenClient(httpClient, options, NullLogger<KeycloakTokenClient>.Instance);
    }

    private static HttpResponseMessage TokenResponse()
    {
        var accessToken = $"{Base64Url("""{"alg":"RS256","typ":"JWT"}""")}." +
                          $"{Base64Url($$"""{"sub":"{{Subject}}","preferred_username":"p@cauce.local"}""")}.firma";

        var body = $$"""
            {
              "access_token": "{{accessToken}}",
              "refresh_token": "refresh-nuevo",
              "expires_in": 900,
              "refresh_expires_in": 2592000,
              "token_type": "Bearer"
            }
            """;

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
    }

    private static HttpResponseMessage ErrorResponse(HttpStatusCode status, string error)
    {
        return new HttpResponseMessage(status)
        {
            Content = new StringContent($$"""{"error":"{{error}}"}""", Encoding.UTF8, "application/json")
        };
    }

    private static string Base64Url(string json)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    /// <summary>
    /// Manejador que captura el formulario enviado y devuelve una respuesta fija.
    /// </summary>
    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public CapturingHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        public Dictionary<string, string> LastForm { get; private set; } = [];

        public int CallCount { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            LastForm = body
                .Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Select(pair => pair.Split('=', 2))
                .ToDictionary(
                    parts => Uri.UnescapeDataString(parts[0]),
                    parts => Uri.UnescapeDataString(parts.Length > 1 ? parts[1].Replace('+', ' ') : string.Empty));

            return _response;
        }
    }
}
