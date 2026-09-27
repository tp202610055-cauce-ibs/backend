using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cauce.Application.Common.Identity;

/// <summary>
/// Arma el contexto adicional (<c>additional_context</c>) de las filas de auditoría de sesión: inicio,
/// renovación y cierre. Lo usan el middleware de auditoría y el handler de renovación, para que las
/// filas de los dos mecanismos tengan la misma forma: <c>{"channel", "cause", "clientId"}</c> (acta A68).
/// </summary>
public static class AuthAuditContext
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Devuelve el nombre con el que se registra el canal en la auditoría.
    /// </summary>
    /// <param name="channel">Canal de la petición.</param>
    /// <returns><c>mobile</c> o <c>portal</c>.</returns>
    public static string ChannelName(LoginChannel channel)
    {
        return channel == LoginChannel.Portal ? "portal" : "mobile";
    }

    /// <summary>
    /// Serializa el contexto de una fila de auditoría de sesión. Los valores nulos se omiten.
    /// </summary>
    /// <param name="channel">Canal de la petición.</param>
    /// <param name="cause">Causa interna del rechazo, o <see langword="null"/> si no hubo rechazo.</param>
    /// <param name="clientId">Cliente OIDC informado en la petición, o <see langword="null"/> si no aplica.</param>
    /// <returns>El JSON del contexto adicional.</returns>
    public static string Build(LoginChannel channel, string? cause, string? clientId = null)
    {
        return JsonSerializer.Serialize(
            new AuthAuditContextPayload(ChannelName(channel), cause, clientId),
            SerializerOptions);
    }

    private sealed record AuthAuditContextPayload(
        [property: JsonPropertyName("channel")] string Channel,
        [property: JsonPropertyName("cause")] string? Cause,
        [property: JsonPropertyName("clientId")] string? ClientId);
}
