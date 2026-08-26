namespace Cauce.Infrastructure.Email;

/// <summary>
/// Configuración del envío de correos transaccionales. Se vincula a la sección
/// <c>Email</c> de la configuración de la aplicación.
/// </summary>
public sealed class EmailOptions
{
    /// <summary>
    /// Nombre de la sección de configuración asociada a estas opciones.
    /// </summary>
    public const string SectionName = "Email";

    /// <summary>
    /// Host del servidor SMTP.
    /// </summary>
    public required string SmtpHost { get; init; }

    /// <summary>
    /// Puerto del servidor SMTP.
    /// </summary>
    public required int SmtpPort { get; init; }

    /// <summary>
    /// Indica si la conexión SMTP usa SSL/TLS.
    /// </summary>
    public bool UseSsl { get; init; }

    /// <summary>
    /// Usuario de autenticación SMTP, si aplica.
    /// </summary>
    public string? Username { get; init; }

    /// <summary>
    /// Contraseña de autenticación SMTP, si aplica.
    /// </summary>
    public string? Password { get; init; }

    /// <summary>
    /// Dirección de correo del remitente.
    /// </summary>
    public required string FromAddress { get; init; }

    /// <summary>
    /// Nombre visible del remitente.
    /// </summary>
    public required string FromName { get; init; }

    /// <summary>
    /// URL base de la app móvil, usada para construir enlaces dirigidos al paciente. En el piloto es
    /// un esquema de deep link (<c>cauce://</c>), no una URL http, porque el destino es una pantalla
    /// de la app y no una página web.
    /// </summary>
    public required string MobileAppBaseUrl { get; init; }

    /// <summary>
    /// URL base del portal web, usada para construir enlaces dirigidos al nutricionista.
    /// </summary>
    public required string PortalAppBaseUrl { get; init; }
}
