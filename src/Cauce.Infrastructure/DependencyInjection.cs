using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Infrastructure.Auditing;
using Cauce.Infrastructure.Email;
using Cauce.Infrastructure.Identity;
using Cauce.Infrastructure.Persistence;
using Cauce.Infrastructure.Persistence.Repositories;
using Cauce.Infrastructure.Persistence.Seeders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Cauce.Infrastructure;

/// <summary>
/// Métodos de extensión para registrar los servicios de la capa de infraestructura
/// en el contenedor de inyección de dependencias.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registra el contexto de base de datos, las opciones, los repositorios, los
    /// servicios de identidad y correo, y los seeders de la capa de infraestructura.
    /// </summary>
    /// <param name="services">Colección de servicios a configurar.</param>
    /// <param name="configuration">Configuración de la aplicación.</param>
    /// <returns>La misma colección de servicios para encadenamiento.</returns>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<CauceDbContext>(options =>
        {
            options
                .UseNpgsql(configuration.GetConnectionString("Cauce"))
                .UseSnakeCaseNamingConvention();
        });

        // Opciones.
        services.Configure<KeycloakOptions>(configuration.GetSection(KeycloakOptions.SectionName));
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));
        services.Configure<ConsentDocumentOptions>(configuration.GetSection(ConsentDocumentOptions.SectionName));
        services.Configure<DevAdminOptions>(configuration.GetSection(DevAdminOptions.SectionName));

        // Servicios transversales.
        services.AddHttpContextAccessor();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IAuditLogger, AuditLogger>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        // Servicios de identidad y correo.
        services.AddSingleton<IConsentService, ConsentService>();
        services.AddSingleton<IPasswordResetTokenGenerator, PasswordResetTokenGenerator>();
        services.AddSingleton<ITemporaryPasswordGenerator, TemporaryPasswordGenerator>();
        services.AddSingleton<IClientUrlProvider, ClientUrlProvider>();
        services.AddScoped<IEmailSender, SmtpEmailSender>();

        // Repositorios del módulo de identidad.
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IInvitationCodeRepository, InvitationCodeRepository>();
        services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();
        services.AddScoped<IConsentRecordRepository, ConsentRecordRepository>();

        // Seeders.
        services.AddScoped<UserRolesSeeder>();
        services.AddScoped<DevAdminSeeder>();

        // Cliente de administración de Keycloak (cliente HTTP tipado).
        services.AddHttpClient<IKeycloakAdminClient, KeycloakAdminClient>();

        return services;
    }
}
