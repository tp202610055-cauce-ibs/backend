using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cauce.Infrastructure.Notifications.Senders;

/// <summary>
/// Crea el cliente de Firebase Cloud Messaging una sola vez. Si la credencial falta o es inválida, lo
/// registra como error y devuelve <see langword="null"/>: la API sigue arriba y el push falla controlado.
/// </summary>
public sealed class FirebaseMessagingProvider
{
    private readonly Lazy<FirebaseMessaging?> _messaging;

    /// <summary>
    /// Inicializa el proveedor con la configuración de notificaciones.
    /// </summary>
    public FirebaseMessagingProvider(IOptions<NotificationOptions> options, ILogger<FirebaseMessagingProvider> logger)
    {
        _messaging = new Lazy<FirebaseMessaging?>(
            () => Create(options.Value.Fcm.CredentialsPath, logger),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>
    /// Cliente de FCM listo para enviar, o <see langword="null"/> si no se pudo inicializar.
    /// </summary>
    public FirebaseMessaging? Messaging => _messaging.Value;

    private static FirebaseMessaging? Create(string? credentialsPath, ILogger logger)
    {
        try
        {
            var app = FirebaseApp.DefaultInstance ?? FirebaseApp.Create(new AppOptions
            {
                Credential = string.IsNullOrWhiteSpace(credentialsPath)
                    ? GoogleCredential.GetApplicationDefault()
                    : GoogleCredential.FromFile(credentialsPath)
            });
            return FirebaseMessaging.GetMessaging(app);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Firebase Cloud Messaging could not be initialized from {CredentialsSetting}; push notifications will fail until the credentials file is fixed and the API is restarted.",
                "Notifications__Fcm__CredentialsPath");
            return null;
        }
    }
}
