namespace Cauce.Api.Configuration;

/// <summary>
/// Configuración de los forwarded headers para cuando la API corre detrás de un proxy inverso, como el
/// NGINX del despliegue (acta A68). Sin ella, la IP que registra <c>audit_logs</c> y la que particiona el
/// rate limiting sería la del proxy y no la del usuario. Se vincula a la sección <c>ForwardedHeaders</c>.
/// </summary>
public sealed class ForwardedHeadersSettings
{
    /// <summary>
    /// Nombre de la sección de configuración asociada a estas opciones.
    /// </summary>
    public const string SectionName = "ForwardedHeaders";

    /// <summary>
    /// Indica si se aplican los headers <c>X-Forwarded-For</c> y <c>X-Forwarded-Proto</c>. Está apagado
    /// por defecto: sin un proxy delante, cualquiera podría falsear su IP con esos headers.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// Cantidad máxima de proxies que se recorren en <c>X-Forwarded-For</c>. Con un solo NGINX es 1.
    /// </summary>
    public int ForwardLimit { get; init; } = 1;

    /// <summary>
    /// Direcciones IP de los proxies de confianza, por ejemplo <c>10.0.0.5</c>. Solo se aceptan los
    /// headers que llegan desde una de ellas o desde una red de <see cref="KnownNetworks"/>.
    /// </summary>
    public string[] KnownProxies { get; init; } = [];

    /// <summary>
    /// Redes de confianza en notación CIDR, por ejemplo <c>172.18.0.0/16</c> para la red de Docker.
    /// </summary>
    public string[] KnownNetworks { get; init; } = [];
}
