using System.Text;
using Cauce.Api.Configuration;
using Cauce.Api.Domain.Entities;
using Cauce.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ===========================================================================
// 1) Servicios de presentación (capa Controllers)
// ===========================================================================
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// ===========================================================================
// 2) Persistencia (capa Infrastructure: EF Core + PostgreSQL)
// ===========================================================================
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'DefaultConnection' no configurada en appsettings.json.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// ===========================================================================
// 3) ASP.NET Core Identity (adenda DEC-009)
//
// AddIdentityCore es la variante minimalista de Identity, recomendada para
// APIs JWT-only (sin sesiones por cookies). Por adenda DEC-009, Identity
// sustituye temporalmente a Keycloak hasta la versión v0.2.0.
//
// Las políticas de contraseña y bloqueo se alinean con los criterios de
// aceptación de los US01 (registro) y US05 (login) del Product Backlog v5.
// ===========================================================================
builder.Services.AddIdentityCore<User>(options =>
{
    // US01 CA01: contraseña mínimo 8 caracteres, al menos 1 mayúscula y 1 número.
    options.Password.RequireDigit = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 8;

    // US05 CA02: bloqueo tras 5 intentos fallidos consecutivos.
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.AllowedForNewUsers = true;

    // Diseño OE2: el email es identificador único del usuario.
    options.User.RequireUniqueEmail = true;
})
.AddRoles<IdentityRole<Guid>>()
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddSignInManager()
.AddDefaultTokenProviders();

// ===========================================================================
// 4) Configuración tipada de JWT (binding desde appsettings.json)
// ===========================================================================
builder.Services.Configure<JwtOptions>(
    builder.Configuration.GetSection(JwtOptions.SectionName));

var jwtOptions = builder.Configuration
    .GetSection(JwtOptions.SectionName)
    .Get<JwtOptions>()
    ?? throw new InvalidOperationException(
        "Sección 'Jwt' no configurada en appsettings.json.");

// ===========================================================================
// 5) Autenticación con JWT Bearer
// ===========================================================================
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtOptions.Issuer,
        ValidAudience = jwtOptions.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

// ===========================================================================
// 6) Swagger UI con soporte para autenticación JWT Bearer
// ===========================================================================
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Cauce.Api",
        Version = "v1",
        Description = "API backend del sistema mHealth de recomendaciones " +
                      "dietéticas para pacientes con Síndrome de Intestino Irritable. " +
                      "Tesis UPC 2026 - Trigueros, Contreras."
    });

    // Esquema de seguridad: permite probar endpoints autenticados desde Swagger UI.
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Pega solo el token JWT (sin el prefijo 'Bearer ')."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// ===========================================================================
// Construcción de la aplicación
// ===========================================================================
var app = builder.Build();

// ===========================================================================
// Pipeline HTTP
// ===========================================================================
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();