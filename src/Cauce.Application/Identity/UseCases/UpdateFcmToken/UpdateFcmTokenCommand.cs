using MediatR;

namespace Cauce.Application.Identity.UseCases.UpdateFcmToken;

/// <summary>
/// Comando para registrar o actualizar el token de notificaciones push (Firebase Cloud Messaging) del
/// dispositivo del usuario autenticado (TS10 CA01).
/// </summary>
/// <param name="FcmToken">Token de FCM del dispositivo, o <see langword="null"/> para desvincularlo.</param>
public sealed record UpdateFcmTokenCommand(string? FcmToken) : IRequest<Unit>;
