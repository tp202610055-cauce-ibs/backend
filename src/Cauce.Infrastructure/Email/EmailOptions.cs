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
    /// URL base de la aplicación, usada para construir enlaces de cara al cliente.
    /// </summary>
    public required string AppBaseUrl { get; init; }
}
