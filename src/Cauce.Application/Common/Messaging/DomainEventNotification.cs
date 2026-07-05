using Cauce.Domain.Common;
using MediatR;

namespace Cauce.Application.Common.Messaging;

/// <summary>
/// Envoltura genérica que adapta un evento de dominio (<see cref="IDomainEvent"/>, sin dependencia
/// de MediatR) a una notificación de MediatR (<see cref="INotification"/>), para que el
/// <c>OutboxDispatcher</c> lo publique por el bus (acta A9). Los handlers se implementan como
/// <c>INotificationHandler&lt;DomainEventNotification&lt;TEvent&gt;&gt;</c>.
/// </summary>
/// <typeparam name="TEvent">Tipo del evento de dominio envuelto.</typeparam>
/// <param name="DomainEvent">Evento de dominio.</param>
public sealed record DomainEventNotification<TEvent>(TEvent DomainEvent) : INotification
    where TEvent : IDomainEvent;
