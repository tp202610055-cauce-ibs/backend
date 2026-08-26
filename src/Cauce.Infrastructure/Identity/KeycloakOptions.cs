namespace Cauce.Infrastructure.Identity;

/// <summary>
/// Configuración de integración con Keycloak como proveedor de identidad (IdP).
/// Se vincula a la sección <c>Keycloak</c> de la configuración de la aplicación.
/// </summary>
public sealed class KeycloakOptions
{
    /// <summary>
    /// Nombre de la sección de configuración asociada a estas opciones.
    /// </summary>
    public const string SectionName = "Keycloak";

    /// <summary>
    /// Autoridad emisora de los tokens (URL del realm), por ejemplo
    /// <c>http://localhost:8081/realms/cauce</c>.
    /// </summary>
    public required string Authority { get; init; }

    /// <summary>
    /// Nombre del realm configurado en Keycloak, por ejemplo <c>cauce</c>.
    /// </summary>
    public required string Realm { get; init; }

    /// <summary>
    /// Audiencia esperada en el claim <c>aud</c> del token, por ejemplo
    /// <c>cauce-backend</c>.
    /// </summary>
    public required string Audience { get; init; }

    /// <summary>
    /// Identificador del cliente confidencial del backend en Keycloak.
    /// </summary>
    public required string ClientId { get; init; }

    /// <summary>
    /// Secreto del cliente confidencial. Se provee vía User Secrets o variables
    /// de entorno; nunca se versiona en <c>appsettings</c>.
    /// </summary>
    public required string ClientSecret { get; init; }

    /// <summary>
    /// Indica si la obtención de metadatos OIDC exige HTTPS. Es <see langword="false"/>
    /// en desarrollo y <see langword="true"/> en producción.
    /// </summary>
    public bool RequireHttpsMetadata { get; init; }

    /// <summary>
    /// Segundos de espera que el realm añade tras alcanzar el umbral de intentos fallidos. Debe
    /// reflejar el <c>waitIncrementSeconds</c> configurado en Keycloak: se usa para calcular, a partir
    /// del último fallo, hasta cuándo permanece bloqueada la cuenta (US05 CA02).
    /// </summary>
    public int WaitIncrementSeconds { get; init; } = 60;

    /// <summary>
    /// Dirección del documento de descubrimiento OIDC, derivada de
    /// <see cref="Authority"/>.
    /// </summary>
    public string MetadataAddress => $"{Authority}/.well-known/openid-configuration";
}
