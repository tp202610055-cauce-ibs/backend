using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Asp.Versioning;
using Cauce.Api.Configuration;
using Cauce.Api.Middleware;
using Cauce.Api.Workers;
using Cauce.Application;
using Cauce.Infrastructure;
using Cauce.Infrastructure.Identity;
using Cauce.Infrastructure.Persistence.Seeders;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using QuestPDF.Infrastructure;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Licencia comunitaria de QuestPDF (uso académico / sin fines de lucro), fijada antes de generar PDFs.
QuestPDF.Settings.License = LicenseType.Community;

// 2. Serilog como logger desde el inicio, configurado a partir de la configuración.
builder.Host.UseSerilog((context, loggerConfiguration) =>
    loggerConfiguration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithEnvironmentName());

// 3. Fuentes de configuración adicionales: User Secrets en desarrollo.
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>(optional: true);
}

// 4. Inyección de dependencias por capa.
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// 4.b Workers en segundo plano (DEC-B5-04/06). Cada uno puede deshabilitarse por configuración
// (Workers:{Name}:Enabled) para las pruebas de integración.
builder.Services.AddHostedService<OutboxDispatcherWorker>();
builder.Services.AddHostedService<NotificationDispatcherWorker>();
builder.Services.AddHostedService<RecommendationExpirationWorker>();
builder.Services.AddHostedService<RecommendationArchivalWorker>();
builder.Services.AddHostedService<OutboxRetentionWorker>();
builder.Services.AddHostedService<WeeklyRecommendationReminderWorker>();
builder.Services.AddHostedService<IbsSssReminderWorker>();

// Clave de API administrativa (propia de la capa API).
builder.Services.Configure<AdminApiKeyOptions>(
    builder.Configuration.GetSection(AdminApiKeyOptions.SectionName));

// Opciones de Keycloak para configurar la validación de JWT.
var keycloakOptions = builder.Configuration
    .GetSection(KeycloakOptions.SectionName)
    .Get<KeycloakOptions>()
    ?? throw new InvalidOperationException("Falta la sección de configuración 'Keycloak'.");

// 5. Autenticación JWT Bearer contra el realm de Keycloak.
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = keycloakOptions.Authority;
        options.Audience = keycloakOptions.Audience;
        options.RequireHttpsMetadata = keycloakOptions.RequireHttpsMetadata;
        options.MetadataAddress = keycloakOptions.MetadataAddress;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = keycloakOptions.Authority,
            ValidateAudience = true,
            ValidAudience = keycloakOptions.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            NameClaimType = "preferred_username",
            RoleClaimType = ClaimTypes.Role
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = MapKeycloakRealmRoles
        };
    });

// 6. Autorización con políticas por rol.
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("Patient", policy => policy.RequireRole("patient"))
    .AddPolicy("Nutritionist", policy => policy.RequireRole("nutritionist"));

// 7. CORS según DEC-B3-02.
var allowedOrigins = (builder.Configuration["Cors:AllowedOrigins"] ?? string.Empty)
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
{
    options.AddPolicy("CaucePortalPolicy", policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
            .WithHeaders("Authorization", "Content-Type", "Idempotency-Key", "X-Client-Guid")
            .AllowCredentials();
    });
});

// 8. Rate limiting según DEC-B3-03.
builder.Services.AddRateLimiter(RateLimitingPolicies.Configure);

// 9. Versionado de API basado en URL (DEC-B3-05).
builder.Services
    .AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ReportApiVersions = true;
        options.ApiVersionReader = new UrlSegmentApiVersionReader();
    })
    .AddMvc()
    .AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });

// 10. Controladores con configuración de serialización JSON.
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// 11. Swagger / OpenAPI con seguridad JWT Bearer.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Cauce API",
        Version = "v1",
        Description = "API del sistema de recomendaciones dietéticas para pacientes con SII.",
        Contact = new OpenApiContact
        {
            Name = "Equipo Cauce",
            Email = "soporte@cauce.local"
        },
        License = new OpenApiLicense
        {
            Name = "Uso académico — Tesis de pregrado"
        }
    });

    var jwtSecurityScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Description = "Token JWT emitido por Keycloak. Formato: Bearer {token}.",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Reference = new OpenApiReference
        {
            Id = JwtBearerDefaults.AuthenticationScheme,
            Type = ReferenceType.SecurityScheme
        }
    };

    options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, jwtSecurityScheme);
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { jwtSecurityScheme, Array.Empty<string>() }
    });

    // Documentación de endpoints a partir de los comentarios XML del ensamblado.
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

// 12. Health checks básicos.
builder.Services.AddHealthChecks();

// 13. Construcción de la aplicación.
var app = builder.Build();

// 14. Pipeline de middleware.
app.UseSerilogRequestLogging();
app.UseExceptionHandlingMiddleware();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Cauce API v1");
        options.RoutePrefix = "swagger";
    });
}

if (keycloakOptions.RequireHttpsMetadata)
{
    app.UseHttpsRedirection();
}

app.UseCors("CaucePortalPolicy");

// Rate limiting activable por configuración (DEC-B3-03). En Development se desactiva por defecto
// (appsettings.Development.json) para no chocar con 429 durante el desarrollo local; en Production y
// en las pruebas de integración permanece activo con los thresholds reales. Las políticas siempre se
// registran (AddRateLimiter); solo el middleware de aplicación queda condicionado.
if (app.Configuration.GetValue("RateLimiting:Enabled", true))
{
    // Antes del limitador: la política de auth-verify-resend particiona por el correo del cuerpo, y las
    // fábricas de partición son síncronas, así que alguien tiene que bufferizarlo y dejarlo resuelto
    // antes (acta A44).
    app.UseVerificationResendPartition();
    app.UseRateLimiter();
}

app.UseAuthentication();
app.UseAuthorization();

// Auditoría de la capa HTTP (LOGIN/LOGOUT/FAILED_LOGIN): tras la autorización, para que el logout
// disponga del principal y pueda resolver el actor (DEC-B5-01 capa 1).
app.UseAuditingMiddleware();

app.MapControllers();
app.MapHealthChecks("/api/v1/health/live");

// Sembrado automático en desarrollo: aplica migraciones, puebla el catálogo de
// roles y provisiona el nutricionista de prueba.
if (app.Environment.IsDevelopment())
{
    await app.Services.RunDevelopmentSeedAsync();
}

// 15. Ejecución.
app.Run();

// Mapea los roles del realm de Keycloak, presentes en el claim realm_access como objeto JSON,
// a claims de rol estándar de ASP.NET Core para que funcione la autorización basada en
// [Authorize(Roles = "...")] y las políticas por rol.
static Task MapKeycloakRealmRoles(TokenValidatedContext context)
{
    if (context.Principal?.Identity is not ClaimsIdentity identity)
    {
        return Task.CompletedTask;
    }

    var realmAccess = identity.FindFirst("realm_access")?.Value;
    if (string.IsNullOrWhiteSpace(realmAccess))
    {
        return Task.CompletedTask;
    }

    try
    {
        using var document = JsonDocument.Parse(realmAccess);
        if (document.RootElement.TryGetProperty("roles", out var rolesElement)
            && rolesElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var role in rolesElement.EnumerateArray())
            {
                var value = role.GetString();
                if (!string.IsNullOrEmpty(value))
                {
                    identity.AddClaim(new Claim(ClaimTypes.Role, value));
                }
            }
        }
    }
    catch (JsonException)
    {
        // Un claim realm_access malformado no debe impedir la autenticación;
        // simplemente no se mapean roles.
    }

    return Task.CompletedTask;
}

/// <summary>
/// Punto de entrada de la aplicación expuesto como clase parcial pública para
/// permitir que las pruebas de integración la referencien con
/// <c>WebApplicationFactory</c>.
/// </summary>
public partial class Program
{
}
