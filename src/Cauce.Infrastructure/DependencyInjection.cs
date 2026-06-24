using Cauce.Application.Common.Interfaces;
using Cauce.Infrastructure.Auditing;
using Cauce.Infrastructure.Identity;
using Cauce.Infrastructure.Persistence;
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
    /// Registra el contexto de base de datos, las opciones de Keycloak y las
    /// implementaciones de los servicios transversales (unidad de trabajo,
    /// registro de auditoría, usuario actual y cliente de administración de Keycloak).
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

        services.Configure<KeycloakOptions>(configuration.GetSection(KeycloakOptions.SectionName));

        services.AddHttpContextAccessor();

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IAuditLogger, AuditLogger>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        services.AddHttpClient<KeycloakAdminClient>(client =>
        {
            var authority = configuration.GetSection(KeycloakOptions.SectionName)["Authority"];
            if (!string.IsNullOrWhiteSpace(authority))
            {
                client.BaseAddress = new Uri(authority);
            }
        });

        return services;
    }
}
