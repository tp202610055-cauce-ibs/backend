namespace Cauce.Api.Contracts.Identity;

/// <summary>
/// Cuerpo de la petición de registro del token de notificaciones push del dispositivo.
/// </summary>
/// <param name="FcmToken">Token de FCM del dispositivo, o <see langword="null"/> para desvincularlo.</param>
public sealed record UpdateFcmTokenRequest(string? FcmToken);
