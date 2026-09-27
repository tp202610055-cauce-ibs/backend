using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using IPNetwork = Microsoft.AspNetCore.HttpOverrides.IPNetwork;

namespace Cauce.Api.Configuration;

/// <summary>
/// Registra y aplica los forwarded headers según <see cref="ForwardedHeadersSettings"/> (acta A68). La
/// configuración se lee al construir las opciones y no al registrar los servicios, para que valga la de
/// cada ambiente aunque se agregue después, como hacen las pruebas de integración.
/// </summary>
public static class ForwardedHeadersSetup
{
    /// <summary>
    /// Registra las opciones de forwarded headers con los proxies y redes de confianza configurados.
    /// </summary>
    /// <param name="services">Colección de servicios.</param>
    /// <returns>La misma colección, para encadenar llamadas.</returns>
    public static IServiceCollection AddCauceForwardedHeaders(this IServiceCollection services)
    {
        services
            .AddOptions<ForwardedHeadersOptions>()
            .Configure<IConfiguration>((options, configuration) => Apply(options, ReadSettings(configuration)));

        return services;
    }

    /// <summary>
    /// Agrega el middleware de forwarded headers si está habilitado. Tiene que ser el primero del
    /// pipeline, para que el log de peticiones, el rate limiting y la auditoría vean ya la IP del usuario.
    /// Si está habilitado sin ningún proxy ni red, falla al arrancar: ASP.NET Core confiaría en cualquier
    /// origen y la IP de la auditoría se podría falsear con un header.
    /// </summary>
    /// <param name="app">Aplicación web.</param>
    /// <returns>La misma aplicación, para encadenar llamadas.</returns>
    /// <exception cref="InvalidOperationException">
    /// Si están habilitados sin proxies ni redes, o si alguna dirección o red no es válida.
    /// </exception>
    public static WebApplication UseCauceForwardedHeaders(this WebApplication app)
    {
        if (!ReadSettings(app.Configuration).Enabled)
        {
            return app;
        }

        // Resolver las opciones ahora valida la configuración al arrancar y no en la primera petición.
        _ = app.Services.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;
        app.UseForwardedHeaders();
        return app;
    }

    /// <summary>
    /// Traslada la configuración a las opciones de ASP.NET Core. Apagado, no toca nada.
    /// </summary>
    /// <param name="options">Opciones de forwarded headers.</param>
    /// <param name="settings">Configuración leída.</param>
    /// <exception cref="InvalidOperationException">
    /// Si están habilitados sin proxies ni redes, o si alguna dirección o red no es válida.
    /// </exception>
    internal static void Apply(ForwardedHeadersOptions options, ForwardedHeadersSettings settings)
    {
        if (!settings.Enabled)
        {
            return;
        }

        if (settings.KnownProxies.Length == 0 && settings.KnownNetworks.Length == 0)
        {
            throw new InvalidOperationException(
                "ForwardedHeaders:Enabled exige al menos un proxy en ForwardedHeaders:KnownProxies o una red en "
                + "ForwardedHeaders:KnownNetworks.");
        }

        var proxies = settings.KnownProxies.Select(ParseProxy).ToList();
        var networks = settings.KnownNetworks.Select(ParseNetwork).ToList();

        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = settings.ForwardLimit;

        // Los valores por defecto confían en loopback; se reemplazan por los configurados.
        options.KnownProxies.Clear();
        options.KnownNetworks.Clear();
        foreach (var proxy in proxies)
        {
            options.KnownProxies.Add(proxy);
        }

        foreach (var network in networks)
        {
            options.KnownNetworks.Add(network);
        }
    }

    private static ForwardedHeadersSettings ReadSettings(IConfiguration configuration)
    {
        return configuration.GetSection(ForwardedHeadersSettings.SectionName).Get<ForwardedHeadersSettings>()
            ?? new ForwardedHeadersSettings();
    }

    private static IPAddress ParseProxy(string value)
    {
        return IPAddress.TryParse(value.Trim(), out var address)
            ? address
            : throw new InvalidOperationException(
                $"ForwardedHeaders:KnownProxies contiene una IP inválida: '{value}'.");
    }

    private static IPNetwork ParseNetwork(string value)
    {
        var parts = value.Trim().Split('/');
        if (parts.Length == 2
            && IPAddress.TryParse(parts[0], out var prefix)
            && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var length)
            && length >= 0
            && length <= (prefix.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 ? 128 : 32))
        {
            return new IPNetwork(prefix, length);
        }

        throw new InvalidOperationException($"ForwardedHeaders:KnownNetworks contiene una red inválida: '{value}'.");
    }
}
