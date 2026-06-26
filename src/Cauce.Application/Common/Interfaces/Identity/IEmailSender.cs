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
}
