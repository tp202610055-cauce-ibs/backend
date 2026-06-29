using System.Security.Claims;
using Cauce.Application.Common.Interfaces.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Cauce.Api.IntegrationTests.Identity.Support;

/// <summary>
/// Fábrica de aplicación para pruebas de integración. Apunta el contexto a un
/// PostgreSQL real, sustituye Keycloak y el envío de correo por dobles en memoria,
/// y reconfigura la validación de JWT para aceptar tokens firmados localmente.
/// </summary>
public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    /// <summary>
    /// Versión del documento de consentimiento usada en pruebas.
    /// </summary>
    public const string ConsentVersion = "1.0";

    /// <summary>
    /// Texto del documento de consentimiento usado en pruebas.
    /// </summary>
    public const string ConsentText = "Texto de consentimiento informado de prueba para Cauce.";

    /// <summary>
    /// Clave de API administrativa usada en pruebas.
    /// </summary>
    public const string AdminApiKey = "integration-test-admin-api-key-0123456789";

    private readonly string _connectionString;
    private readonly string _redisConnectionString;

    /// <summary>
    /// Inicializa la fábrica con las cadenas de conexión de los contenedores.
    /// </summary>
    /// <param name="connectionString">Cadena de conexión a PostgreSQL.</param>
    /// <param name="redisConnectionString">Cadena de conexión a Redis/KeyDB, opcional.</param>
    public CustomWebApplicationFactory(string connectionString, string? redisConnectionString = null)
    {
        _connectionString = connectionString;
        _redisConnectionString = redisConnectionString ?? "localhost:6379";
    }

    /// <summary>
    /// Doble en memoria del cliente de Keycloak.
    /// </summary>
    public FakeKeycloakAdminClient KeycloakClient { get; } = new();

    /// <summary>
    /// Doble en memoria del remitente de correo.
    /// </summary>
    public FakeEmailSender EmailSender { get; } = new();

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Cauce"] = _connectionString,
                ["KeyDb:ConnectionString"] = _redisConnectionString,
                ["Keycloak:Authority"] = "https://test.cauce.local/realms/cauce",
                ["Keycloak:Realm"] = "cauce",
                ["Keycloak:Audience"] = "cauce-backend",
                ["Keycloak:ClientId"] = "cauce-backend",
                ["Keycloak:ClientSecret"] = "test-secret",
                ["Keycloak:RequireHttpsMetadata"] = "false",
                ["Consent:CurrentVersion"] = ConsentVersion,
                ["Consent:Text"] = ConsentText,
                ["Email:SmtpHost"] = "localhost",
                ["Email:SmtpPort"] = "1025",
                ["Email:UseSsl"] = "false",
                ["Email:FromAddress"] = "no-reply@cauce.local",
                ["Email:FromName"] = "Cauce",
                ["Email:AppBaseUrl"] = "http://localhost:5074",
                ["AdminApi:Value"] = AdminApiKey,
                ["DevAdmin:Enabled"] = "false",
                ["Cors:AllowedOrigins"] = "http://localhost:5173"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IKeycloakAdminClient>();
            services.AddSingleton<IKeycloakAdminClient>(KeycloakClient);

            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(EmailSender);

            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.Authority = null;
                options.RequireHttpsMetadata = false;
                // Con una Configuration estática, el handler no consulta metadatos
                // remotos y valida la firma con la clave simétrica de pruebas.
                options.Configuration = new OpenIdConnectConfiguration();
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = TestJwtBuilder.Issuer,
                    ValidateAudience = true,
                    ValidAudience = TestJwtBuilder.Audience,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = TestJwtBuilder.SecurityKey,
                    RoleClaimType = ClaimTypes.Role,
                    NameClaimType = "preferred_username"
                };
            });
        });
    }
}
