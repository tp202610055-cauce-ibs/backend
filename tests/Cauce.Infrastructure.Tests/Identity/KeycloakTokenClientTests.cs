using System.Net;
using System.Text;
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

    private static KeycloakTokenClient CreateClient(CapturingHandler handler)
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

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
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
