namespace Cauce.Application.Common.Interfaces.Identity;

/// <summary>
/// Servicio de envío de correos transaccionales del módulo de identidad.
/// </summary>
public interface IEmailSender
{
    /// <summary>
    /// Envía a un nutricionista recién provisionado sus credenciales temporales.
    /// </summary>
    /// <param name="recipientEmail">Correo del destinatario.</param>
    /// <param name="fullName">Nombre completo del destinatario.</param>
    /// <param name="temporaryPassword">Contraseña temporal asignada.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Tarea que representa la operación asíncrona.</returns>
    Task SendNutritionistTemporaryCredentialsAsync(
        string recipientEmail,
        string fullName,
        string temporaryPassword,
        CancellationToken ct = default);

    /// <summary>
    /// Envía el enlace de restablecimiento de contraseña.
    /// </summary>
    /// <param name="recipientEmail">Correo del destinatario.</param>
    /// <param name="fullName">Nombre completo del destinatario.</param>
    /// <param name="resetLink">Enlace de restablecimiento con el token en claro.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Tarea que representa la operación asíncrona.</returns>
    Task SendPasswordResetLinkAsync(
        string recipientEmail,
        string fullName,
        string resetLink,
        CancellationToken ct = default);

    /// <summary>
    /// Notifica al nutricionista que su reporte clínico está disponible, con la URL prefirmada de
    /// descarga. No contiene la contraseña (se envía por separado).
    /// </summary>
    /// <param name="recipientEmail">Correo del nutricionista.</param>
    /// <param name="fullName">Nombre completo del nutricionista.</param>
    /// <param name="presignedUrl">URL prefirmada de descarga.</param>
    /// <param name="expiresAtUtc">Momento de expiración de la URL, en UTC.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Tarea que representa la operación asíncrona.</returns>
    Task SendReportReadyAsync(
        string recipientEmail,
        string fullName,
        string presignedUrl,
        DateTime expiresAtUtc,
        CancellationToken ct = default);

    /// <summary>
    /// Envía al nutricionista la contraseña del reporte clínico en un correo separado. La
    /// contraseña nunca se persiste ni se registra en logs (DEC-B5-11).
    /// </summary>
    /// <param name="recipientEmail">Correo del nutricionista.</param>
    /// <param name="fullName">Nombre completo del nutricionista.</param>
    /// <param name="password">Contraseña del PDF cifrado.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Tarea que representa la operación asíncrona.</returns>
    Task SendReportPasswordAsync(
        string recipientEmail,
        string fullName,
        string password,
        CancellationToken ct = default);
}
