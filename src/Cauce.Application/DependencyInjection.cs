using Cauce.Application.Common.Behaviors;
using Cauce.Application.Common.Idempotency;
using FluentValidation;
using Mapster;
using MapsterMapper;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Cauce.Application;

/// <summary>
/// Métodos de extensión para registrar los servicios de la capa de aplicación en
/// el contenedor de inyección de dependencias.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registra MediatR con sus comportamientos de pipeline, los validators de
    /// FluentValidation y la configuración de mapeo de Mapster, todos descubiertos
    /// en el ensamblado de la capa de aplicación.
    /// </summary>
    /// <param name="services">Colección de servicios a configurar.</param>
    /// <returns>La misma colección de servicios para encadenamiento.</returns>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var applicationAssembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(configuration =>
        {
            configuration.RegisterServicesFromAssembly(applicationAssembly);
            // El comportamiento de idempotencia debe ir primero: intercepta los reintentos
            // antes de validar o registrar, devolviendo el resultado almacenado.
            configuration.AddOpenBehavior(typeof(IdempotencyBehavior<,>));
            configuration.AddOpenBehavior(typeof(LoggingBehavior<,>));
            configuration.AddOpenBehavior(typeof(ValidationBehavior<,>));
            // La auditoría corre tras la validación (no audita peticiones inválidas) y antes del
            // handler, para las tablas sin trigger marcadas con IAuditableCommand (DEC-B5-01, acta A8).
            configuration.AddOpenBehavior(typeof(AuditingBehavior<,>));
        });

        services.AddValidatorsFromAssembly(applicationAssembly);

        // Contexto de idempotencia con alcance de petición, actualizado por el behavior.
        services.AddScoped<IIdempotencyContext, IdempotencyContext>();

        var typeAdapterConfig = TypeAdapterConfig.GlobalSettings;
        typeAdapterConfig.Scan(applicationAssembly);
        services.AddSingleton(typeAdapterConfig);
        services.AddScoped<IMapper, ServiceMapper>();

        return services;
    }
}
