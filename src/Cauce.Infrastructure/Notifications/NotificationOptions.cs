namespace Cauce.Infrastructure.Notifications;

/// <summary>
/// Configuración del módulo de notificaciones. Se vincula a la sección <c>Notifications</c>.
/// </summary>
public sealed class NotificationOptions
{
    /// <summary>
    /// Nombre de la sección de configuración asociada a estas opciones.
    /// </summary>
    public const string SectionName = "Notifications";

    /// <summary>
    /// Configuración de Firebase Cloud Messaging.
    /// </summary>
    public FcmOptions Fcm { get; init; } = new();
}

/// <summary>
/// Configuración de Firebase Cloud Messaging.
/// </summary>
public sealed class FcmOptions
{
    /// <summary>
    /// Indica si se usa el remitente falso de FCM (desarrollo y pruebas). En este modo no se realiza
    /// ninguna llamada de red y el envío siempre se considera exitoso.
    /// </summary>
    public bool UseFake { get; init; } = true;

    /// <summary>
    /// Ruta al archivo JSON de credenciales de la cuenta de servicio de Firebase, o <see langword="null"/>.
    /// Solo se usa cuando <see cref="UseFake"/> es <see langword="false"/>.
    /// </summary>
    public string? CredentialsPath { get; init; }
}
