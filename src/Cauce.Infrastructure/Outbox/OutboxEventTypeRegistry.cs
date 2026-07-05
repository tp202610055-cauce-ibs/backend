using System.Collections.Frozen;
using System.Text.Json;
using Cauce.Application.Common.Messaging;
using Cauce.Domain.Common;
using Cauce.Domain.Recommendations.Events;
using MediatR;

namespace Cauce.Infrastructure.Outbox;

/// <summary>
/// Resuelve el <see cref="Type"/> de un evento de dominio a partir del nombre completo almacenado en
/// <c>outbox_messages.event_type</c>. Registra los tipos conocidos del ensamblado de dominio para
/// deserializar la carga y despacharla como <c>DomainEventNotification&lt;TEvent&gt;</c>.
/// </summary>
public sealed class OutboxEventTypeRegistry
{
    private readonly FrozenDictionary<string, Type> _typesByName;

    /// <summary>
    /// Inicializa el registro escaneando el ensamblado de dominio en busca de implementaciones de
    /// <see cref="IDomainEvent"/>.
    /// </summary>
    public OutboxEventTypeRegistry()
    {
        var eventTypes = typeof(RecommendationApprovedEvent).Assembly
            .GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && typeof(IDomainEvent).IsAssignableFrom(type));

        var map = new Dictionary<string, Type>(StringComparer.Ordinal);
        foreach (var type in eventTypes)
        {
            if (type.FullName is not null)
            {
                map[type.FullName] = type;
            }
        }

        _typesByName = map.ToFrozenDictionary(StringComparer.Ordinal);
    }

    /// <summary>
    /// Resuelve el tipo del evento por su nombre completo.
    /// </summary>
    /// <param name="eventTypeName">Nombre completo del tipo del evento.</param>
    /// <returns>El tipo del evento, o <see langword="null"/> si no está registrado.</returns>
    public Type? Resolve(string eventTypeName)
    {
        return _typesByName.TryGetValue(eventTypeName, out var type) ? type : null;
    }

    /// <summary>
    /// Deserializa la carga de un mensaje de outbox y la envuelve en un
    /// <see cref="DomainEventNotification{TEvent}"/> listo para publicar en MediatR.
    /// </summary>
    /// <param name="eventTypeName">Nombre completo del tipo del evento.</param>
    /// <param name="payloadJson">Carga serializada del evento.</param>
    /// <returns>La notificación, o <see langword="null"/> si el tipo no está registrado o la carga es inválida.</returns>
    public INotification? ToNotification(string eventTypeName, string payloadJson)
    {
        var eventType = Resolve(eventTypeName);
        if (eventType is null)
        {
            return null;
        }

        var domainEvent = JsonSerializer.Deserialize(payloadJson, eventType, OutboxSerialization.Options);
        if (domainEvent is null)
        {
            return null;
        }

        var notificationType = typeof(DomainEventNotification<>).MakeGenericType(eventType);
        return (INotification?)Activator.CreateInstance(notificationType, domainEvent);
    }
}
