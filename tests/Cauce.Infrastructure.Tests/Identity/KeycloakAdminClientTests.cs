using System.Net;
using System.Text;
using Cauce.Application.Common.Exceptions;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Infrastructure.Identity;
using FluentAssertions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

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

    [Fact]
    public async Task SendUpdatePasswordEmailAsync_KeycloakAccepts_DoesNotThrow()
    {
        var client = CreateClient(new HttpResponseMessage(HttpStatusCode.NoContent));

        var act = async () => await client.SendUpdatePasswordEmailAsync(KeycloakId);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SendUpdatePasswordEmailAsync_KeycloakFails_ThrowsKeycloakIntegrationException()
    {
        // Es lo que responde Keycloak cuando el SMTP del realm no puede enviar el correo.
        var client = CreateClient(new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var act = async () => await client.SendUpdatePasswordEmailAsync(KeycloakId);

        await act.Should().ThrowAsync<KeycloakIntegrationException>();
    }

    [Fact]
    public async Task SendUpdatePasswordEmailAsync_PutsTheUpdatePasswordActionWithoutLifespan()
    {
        var handler = new RoutingHandler(new HttpResponseMessage(HttpStatusCode.NoContent));
        var client = CreateClient(handler);

        await client.SendUpdatePasswordEmailAsync(KeycloakId);

        handler.LastAdminRequestUri!.AbsolutePath
            .Should().EndWith($"/admin/realms/cauce/users/{KeycloakId}/execute-actions-email");
        handler.LastAdminRequestMethod.Should().Be(HttpMethod.Put);
        handler.LastAdminRequestBody.Should().Be("""["UPDATE_PASSWORD"]""");
        // La vigencia del enlace la fija el realm (actionTokenGeneratedByAdminLifespan), no el backend.
        handler.LastAdminRequestUri.Query.Should().BeEmpty();
    }

    [Fact]
    public async Task SendUpdatePasswordEmailAsync_PortalConfigured_SendsPortalClientAndLoginRedirect()
    {
        var handler = new RoutingHandler(new HttpResponseMessage(HttpStatusCode.NoContent));
        var client = CreateClient(handler, portalLoginUrl: "http://localhost:5173/login");

        await client.SendUpdatePasswordEmailAsync(KeycloakId);

        // El enlace del correo termina en el login del portal (acta A68). Sigue sin lifespan: la vigencia
        // la fija el realm.
        var query = QueryHelpers.ParseQuery(handler.LastAdminRequestUri!.Query);
        query["client_id"].ToString().Should().Be("cauce-web-portal");
        query["redirect_uri"].ToString().Should().Be("http://localhost:5173/login");
        query.Should().NotContainKey("lifespan");
        handler.LastAdminRequestBody.Should().Be("""["UPDATE_PASSWORD"]""");
    }

    [Fact]
    public async Task GetUserStateAsync_DisabledWithRequiredActions_ReturnsBoth()
    {
        var client = CreateClient(UserResponse("""{"id":"x","enabled":false,"requiredActions":["UPDATE_PASSWORD"]}"""));

        var state = await client.GetUserStateAsync(KeycloakId);

        state.Enabled.Should().BeFalse();
        state.RequiredActions.Should().Equal("UPDATE_PASSWORD");
    }

    [Fact]
    public async Task GetUserStateAsync_EnabledWithoutActions_ReturnsEnabledAndEmptyActions()
    {
        var client = CreateClient(UserResponse("""{"id":"x","enabled":true,"requiredActions":[]}"""));

        var state = await client.GetUserStateAsync(KeycloakId);

        state.Enabled.Should().BeTrue();
        state.RequiredActions.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserStateAsync_PropertiesMissing_AssumesEnabledWithoutActions()
    {
        // El dato explica un rechazo y no decide ningún acceso: ante la duda, habilitado.
        var client = CreateClient(UserResponse("""{"id":"x"}"""));

        var state = await client.GetUserStateAsync(KeycloakId);

        state.Enabled.Should().BeTrue();
        state.RequiredActions.Should().BeEmpty();
    }

    [Fact]
    public async Task GetUserStateAsync_UserNotFound_ThrowsKeycloakIntegrationException()
    {
        var client = CreateClient(new HttpResponseMessage(HttpStatusCode.NotFound));

        var act = async () => await client.GetUserStateAsync(KeycloakId);

        await act.Should().ThrowAsync<KeycloakIntegrationException>();
    }

    // ----- Compensación del alta y fallas de transporte (acta A70) -----

    [Fact]
    public async Task CreateUserAsync_RoleAssigned_ReturnsTheIdentifierAndDeletesNothing()
    {
        var handler = new ScriptedHandler((request, _) => Task.FromResult(
            IsCreateUser(request) ? Created(NewUserId)
            : IsRoleLookup(request) ? RoleResponse()
            : new HttpResponseMessage(HttpStatusCode.NoContent)));
        var client = CreateClient(handler);

        var keycloakId = await client.CreateUserAsync("paciente@cauce.local", "Ana Pérez", "patient", requireEmailVerification: true);

        keycloakId.Should().Be(NewUserId);
        handler.Requests.Should().NotContain(request => request.Method == HttpMethod.Delete);
    }

    [Fact]
    public async Task CreateUserAsync_RoleAssignmentFails_DeletesTheCreatedUserAndThrows()
    {
        // Es el huérfano que describe el prerequisito 5 del CLAUDE.md: el usuario se crea, el rol no se
        // asigna y el llamador nunca llega a conocer el identificador para compensar.
        var handler = new ScriptedHandler((request, _) => Task.FromResult(
            IsCreateUser(request) ? Created(NewUserId)
            : IsRoleLookup(request) ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
            : new HttpResponseMessage(HttpStatusCode.NoContent)));
        var client = CreateClient(handler);

        var act = async () => await client.CreateUserAsync("paciente@cauce.local", "Ana Pérez", "patient", requireEmailVerification: true);

        await act.Should().ThrowAsync<KeycloakIntegrationException>();
        handler.Requests.Should().ContainSingle(request => request.Method == HttpMethod.Delete)
            .Which.Path.Should().EndWith($"/users/{NewUserId}");
    }

    [Fact]
    public async Task CreateUserAsync_CallerCancelsDuringRoleAssignment_StillDeletesTheCreatedUser()
    {
        using var caller = new CancellationTokenSource();
        var handler = new ScriptedHandler(async (request, token) =>
        {
            if (IsCreateUser(request))
            {
                return Created(NewUserId);
            }

            if (IsRoleLookup(request))
            {
                // El cliente corta la conexión mientras se asigna el rol.
                await caller.CancelAsync();
                await Task.Delay(Timeout.Infinite, token);
            }

            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        var client = CreateClient(handler);

        var act = async () => await client.CreateUserAsync(
            "paciente@cauce.local", "Ana Pérez", "patient", requireEmailVerification: true, caller.Token);

        await act.Should().ThrowAsync<OperationCanceledException>("la cancelación del cliente no se disfraza de 502");
        var delete = handler.Requests.Should().ContainSingle(request => request.Method == HttpMethod.Delete).Subject;
        delete.Path.Should().EndWith($"/users/{NewUserId}");
        delete.CancellationRequested.Should().BeFalse("la compensación no hereda la cancelación de la petición");
    }

    [Fact]
    public async Task DeleteUserAsync_KeycloakUnreachable_ThrowsKeycloakIntegrationException()
    {
        var handler = new ScriptedHandler((_, _) => throw new HttpRequestException("Connection refused (keycloak:8080)"));
        var client = CreateClient(handler);

        var act = async () => await client.DeleteUserAsync(KeycloakId);

        (await act.Should().ThrowAsync<KeycloakIntegrationException>())
            .Which.InnerException.Should().BeOfType<HttpRequestException>();
    }

    [Fact]
    public async Task GetUserStateAsync_TokenEndpointUnreachable_ThrowsKeycloakIntegrationException()
    {
        var handler = new ScriptedHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)))
        {
            TokenFailure = new HttpRequestException("Connection refused (keycloak:8080)")
        };
        var client = CreateClient(handler);

        var act = async () => await client.GetUserStateAsync(KeycloakId);

        await act.Should().ThrowAsync<KeycloakIntegrationException>();
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task DeleteUserAsync_KeycloakDoesNotRespondInTime_ThrowsKeycloakIntegrationException()
    {
        var handler = new ScriptedHandler(async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        var client = CreateClient(handler, timeout: TimeSpan.FromMilliseconds(200));

        var act = async () => await client.DeleteUserAsync(KeycloakId);

        await act.Should().ThrowAsync<KeycloakIntegrationException>();
    }

    [Fact]
    public async Task DeleteUserAsync_CallerCancels_PropagatesTheCancellation()
    {
        using var caller = new CancellationTokenSource();
        var handler = new ScriptedHandler(async (_, token) =>
        {
            await caller.CancelAsync();
            await Task.Delay(Timeout.Infinite, token);
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        var client = CreateClient(handler);

        var act = async () => await client.DeleteUserAsync(KeycloakId, caller.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    private const string NewUserId = "7d3f0c52-2d4e-4a8e-9a57-0f6a3c1f9b11";

    private static bool IsCreateUser(HttpRequestMessage request) =>
        request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath.EndsWith("/users", StringComparison.Ordinal);

    private static bool IsRoleLookup(HttpRequestMessage request) =>
        request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.Contains("/roles/", StringComparison.Ordinal);

    private static HttpResponseMessage Created(string userId)
    {
        var response = new HttpResponseMessage(HttpStatusCode.Created);
        response.Headers.Location = new Uri($"https://test.cauce.local/admin/realms/cauce/users/{userId}");
        return response;
    }

    private static HttpResponseMessage RoleResponse() =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"id":"role-id","name":"patient"}""", Encoding.UTF8, "application/json")
        };

    private static HttpResponseMessage TokenResponse() =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"access_token":"admin-token","expires_in":300}""", Encoding.UTF8, "application/json")
        };

    private static KeycloakAdminClient CreateClient(HttpResponseMessage adminResponse) =>
        CreateClient(new RoutingHandler(adminResponse));

    private static KeycloakAdminClient CreateClient(
        HttpMessageHandler handler,
        string? portalLoginUrl = null,
        TimeSpan? timeout = null)
    {
        var httpClient = new HttpClient(handler);
        if (timeout is not null)
        {
            httpClient.Timeout = timeout.Value;
        }

        var options = Options.Create(new KeycloakOptions
        {
            Authority = "https://test.cauce.local/realms/cauce",
            Realm = "cauce",
            Audience = "cauce-backend",
            ClientId = "cauce-backend",
            ClientSecret = "test-secret"
        });

        var clientUrlProvider = Substitute.For<IClientUrlProvider>();
        clientUrlProvider.BuildPortalLoginUrl().Returns(portalLoginUrl);

        return new KeycloakAdminClient(httpClient, options, clientUrlProvider, NullLogger<KeycloakAdminClient>.Instance);
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

        public string? LastAdminRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/protocol/openid-connect/token", StringComparison.Ordinal))
            {
                return TokenResponse();
            }

            LastAdminRequestUri = request.RequestUri;
            LastAdminRequestMethod = request.Method;
            LastAdminRequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return _adminResponse;
        }
    }

    /// <summary>
    /// Manejador programable: el token de service account se concede siempre (salvo que la prueba programe
    /// una falla) y cada petición a la Admin API la resuelve el guion. Registra método, ruta y si la
    /// cancelación ya estaba pedida al salir, que es lo que distingue una compensación que hereda el token
    /// de la petición de una que no.
    /// </summary>
    private sealed class ScriptedHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _script;

        public ScriptedHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> script)
        {
            _script = script;
        }

        public Exception? TokenFailure { get; init; }

        public List<(HttpMethod Method, string Path, bool CancellationRequested)> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/protocol/openid-connect/token", StringComparison.Ordinal))
            {
                return TokenFailure is null ? Task.FromResult(TokenResponse()) : throw TokenFailure;
            }

            Requests.Add((request.Method, request.RequestUri.AbsolutePath, cancellationToken.IsCancellationRequested));
            return _script(request, cancellationToken);
        }
    }
}
