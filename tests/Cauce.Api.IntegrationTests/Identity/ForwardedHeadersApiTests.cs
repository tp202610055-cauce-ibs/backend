using System.Net;
using System.Net.Http.Json;
using Cauce.Api.Configuration;
using Cauce.Api.IntegrationTests.Identity.Support;
using Cauce.Domain.Auditing.Enums;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;

namespace Cauce.Api.IntegrationTests.Identity;

/// <summary>
/// Pruebas de los forwarded headers configurables (acta A68): detrás del proxy inverso, la IP que registra
/// <c>audit_logs</c> tiene que ser la del usuario y no la del proxy, y nadie fuera de los proxies de
/// confianza puede falsearla con un header. Requieren Docker (PostgreSQL + Redis).
/// </summary>
[Trait("Category", "Integration")]
public sealed class ForwardedHeadersApiTests
    : IntegrationTestBase, IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>
{
    private static readonly IPAddress Proxy = IPAddress.Parse("10.0.0.5");

    /// <summary>
    /// Inicializa la prueba con los fixtures compartidos.
    /// </summary>
    /// <param name="postgres">Fixture del contenedor PostgreSQL.</param>
    /// <param name="redis">Fixture del contenedor Redis/KeyDB.</param>
    public ForwardedHeadersApiTests(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis)
    {
    }

    [SkippableFact]
    public async Task Enabled_RequestFromTrustedProxy_AuditsTheForwardedClientIp()
    {
        SkipIfUnavailable();

        var ip = await FailedLoginIpAsync(enabled: true, remoteIp: Proxy, forwardedFor: "203.0.113.7");

        ip.Should().Be("203.0.113.7");
    }

    [SkippableFact]
    public async Task Disabled_ForwardedHeaderIsIgnored_AuditsTheConnectionIp()
    {
        SkipIfUnavailable();

        var ip = await FailedLoginIpAsync(enabled: false, remoteIp: Proxy, forwardedFor: "203.0.113.7");

        ip.Should().Be("10.0.0.5");
    }

    [SkippableFact]
    public async Task Enabled_RequestFromUntrustedIp_CannotSpoofTheOrigin()
    {
        SkipIfUnavailable();

        var ip = await FailedLoginIpAsync(enabled: true, remoteIp: IPAddress.Parse("198.51.100.9"), forwardedFor: "203.0.113.7");

        ip.Should().Be("198.51.100.9");
    }

    [SkippableFact]
    public async Task Startup_EnabledWithoutProxiesOrNetworks_FailsToStart()
    {
        SkipIfUnavailable();
        await using var factory = new CustomWebApplicationFactory(
            Postgres.ConnectionString,
            Redis.ConnectionString,
            additionalSettings: new Dictionary<string, string?> { ["ForwardedHeaders:Enabled"] = "true" });

        var act = () => factory.CreateClient();

        act.Should().Throw<InvalidOperationException>().WithMessage("*KnownProxies*");
    }

    [Fact]
    public void Apply_InvalidProxyIp_Fails()
    {
        var act = () => ForwardedHeadersSetup.Apply(
            new ForwardedHeadersOptions(),
            new ForwardedHeadersSettings { Enabled = true, KnownProxies = ["no-es-una-ip"] });

        act.Should().Throw<InvalidOperationException>().WithMessage("*no-es-una-ip*");
    }

    [Theory]
    [InlineData("172.18.0.0")]
    [InlineData("172.18.0.0/33")]
    [InlineData("red/16")]
    public void Apply_InvalidNetwork_Fails(string network)
    {
        var act = () => ForwardedHeadersSetup.Apply(
            new ForwardedHeadersOptions(),
            new ForwardedHeadersSettings { Enabled = true, KnownNetworks = [network] });

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Apply_ValidNetworkOnly_ReplacesTheLoopbackDefaults()
    {
        var options = new ForwardedHeadersOptions();

        ForwardedHeadersSetup.Apply(
            options,
            new ForwardedHeadersSettings { Enabled = true, KnownNetworks = ["172.18.0.0/16"] });

        options.KnownProxies.Should().BeEmpty();
        options.KnownNetworks.Should().ContainSingle();
        options.ForwardedHeaders.Should().Be(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);
    }

    [Fact]
    public void Apply_Disabled_IgnoresInvalidValues()
    {
        // Apagado, no se interpreta nada: es el valor por defecto de todos los ambientes.
        var act = () => ForwardedHeadersSetup.Apply(
            new ForwardedHeadersOptions(),
            new ForwardedHeadersSettings { Enabled = false, KnownProxies = ["no-es-una-ip"] });

        act.Should().NotThrow();
    }

    private async Task<string?> FailedLoginIpAsync(bool enabled, IPAddress remoteIp, string forwardedFor)
    {
        var settings = new Dictionary<string, string?>
        {
            ["ForwardedHeaders:Enabled"] = enabled ? "true" : "false",
            ["ForwardedHeaders:KnownProxies:0"] = Proxy.ToString()
        };

        await using var factory = new CustomWebApplicationFactory(
            Postgres.ConnectionString,
            Redis.ConnectionString,
            additionalSettings: settings,
            remoteIpAddress: remoteIp);
        var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/portal/login")
        {
            Content = JsonContent.Create(new { email = "nadie@cauce.local", password = "Incorrecta#1" })
        };
        request.Headers.Add("X-Forwarded-For", forwardedFor);

        var response = await client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var log = (await AuditLogsAsync(action: AuditActionType.FailedLogin)).Should().ContainSingle().Subject;
        return log.IpAddress;
    }
}
