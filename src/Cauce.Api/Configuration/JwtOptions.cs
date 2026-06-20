namespace Cauce.Api.Configuration;

/// <summary>
/// Opciones de configuración para emisión y validación de tokens JWT.
/// Se enlaza con la sección "Jwt" de appsettings.json mediante IOptions&lt;T&gt;.
/// Por adenda DEC-009, ASP.NET Core Identity emite y valida estos tokens
/// temporalmente hasta la reincorporación de Keycloak en v0.2.0.
/// </summary>
public class JwtOptions
{
    /// <summary>Nombre de la sección en appsettings.json.</summary>
    public const string SectionName = "Jwt";

    /// <summary>Emisor del token (claim "iss"). Identifica al backend Cauce.Api.</summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>Audiencia esperada del token (claim "aud"). Identifica al cliente móvil.</summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>Clave simétrica para firma HMAC-SHA256. Mínimo 64 caracteres.</summary>
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>Tiempo de vida del token en minutos.</summary>
    public int ExpirationMinutes { get; set; } = 60;
}