using System.Security.Claims;
using Cauce.Application.Common.Interfaces.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
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
    private readonly string? _ollamaEndpoint;
    private readonly string? _minioEndpoint;
    private readonly string? _minioAccessKey;
    private readonly string? _minioSecretKey;
    private readonly string? _smtpHost;
    private readonly int? _smtpPort;

    /// <summary>
    /// Inicializa la fábrica con las cadenas de conexión de los contenedores.
    /// </summary>
    /// <param name="connectionString">Cadena de conexión a PostgreSQL.</param>
    /// <param name="redisConnectionString">Cadena de conexión a Redis/KeyDB, opcional.</param>
    /// <param name="ollamaEndpoint">Endpoint de Ollama, opcional; apunta a un WireMock en pruebas.</param>
    /// <param name="minioEndpoint">Endpoint de MinIO (host:puerto), opcional.</param>
    /// <param name="minioAccessKey">Clave de acceso de MinIO, opcional.</param>
    /// <param name="minioSecretKey">Clave secreta de MinIO, opcional.</param>
    /// <param name="smtpHost">Host SMTP (Mailpit), opcional; si se provee, el correo de notificación va allí.</param>
    /// <param name="smtpPort">Puerto SMTP (Mailpit), opcional.</param>
    public CustomWebApplicationFactory(
        string connectionString,
        string? redisConnectionString = null,
        string? ollamaEndpoint = null,
        string? minioEndpoint = null,
        string? minioAccessKey = null,
        string? minioSecretKey = null,
        string? smtpHost = null,
        int? smtpPort = null)
    {
        _connectionString = connectionString;
        _redisConnectionString = redisConnectionString ?? "localhost:6379";
        _ollamaEndpoint = ollamaEndpoint;
        _minioEndpoint = minioEndpoint;
        _minioAccessKey = minioAccessKey;
        _minioSecretKey = minioSecretKey;
        _smtpHost = smtpHost;
        _smtpPort = smtpPort;
    }

    /// <summary>
    /// Doble en memoria del cliente de administración de Keycloak.
    /// </summary>
    public FakeKeycloakAdminClient KeycloakClient { get; } = new();

    /// <summary>
    /// Doble en memoria del cliente de tokens de Keycloak (login/logout passthrough).
    /// </summary>
    public FakeKeycloakTokenClient TokenClient { get; } = new();

    /// <summary>
    /// Doble en memoria del remitente de correo.
    /// </summary>
    public FakeEmailSender EmailSender { get; } = new();

    /// <summary>
    /// Reloj controlable inyectado como <see cref="TimeProvider"/>, para simular el paso del tiempo en
    /// pruebas (por ejemplo, la ventana de backoff del outbox).
    /// </summary>
    public FakeTimeProvider Clock { get; } = new(DateTimeOffset.UtcNow);

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            var settings = new Dictionary<string, string?>
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
                ["Cors:AllowedOrigins"] = "http://localhost:5173",
                // Los workers se deshabilitan: las pruebas invocan los processors directamente para ser
                // deterministas (acta A12).
                ["Workers:OutboxDispatcher:Enabled"] = "false",
                ["Workers:NotificationDispatcher:Enabled"] = "false",
                ["Workers:RecommendationExpiration:Enabled"] = "false",
                ["Workers:OutboxRetention:Enabled"] = "false",
                ["Workers:WeeklyReminder:Enabled"] = "false",
                ["Notifications:Fcm:UseFake"] = "true"
            };

            if (_ollamaEndpoint is not null)
            {
                settings["Recommendations:Ollama:Endpoint"] = _ollamaEndpoint;
                settings["Recommendations:Ollama:TimeoutSeconds"] = "2";
            }

            if (_minioEndpoint is not null)
            {
                settings["Storage:Minio:Endpoint"] = _minioEndpoint;
                settings["Storage:Minio:AccessKey"] = _minioAccessKey;
                settings["Storage:Minio:SecretKey"] = _minioSecretKey;
                settings["Storage:Minio:UseSsl"] = "false";
                settings["Storage:Minio:ReportsBucket"] = "clinical-reports";
            }

            if (_smtpHost is not null)
            {
                // Correo de notificación (SmtpEmailNotificationSender) hacia Mailpit real.
                settings["Email:SmtpHost"] = _smtpHost;
                settings["Email:SmtpPort"] = _smtpPort?.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            configuration.AddInMemoryCollection(settings);
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IKeycloakAdminClient>();
            services.AddSingleton<IKeycloakAdminClient>(KeycloakClient);

            services.RemoveAll<IKeycloakTokenClient>();
            services.AddSingleton<IKeycloakTokenClient>(TokenClient);

            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(EmailSender);

            // Reloj controlable para las pruebas del backoff del outbox.
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);

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
