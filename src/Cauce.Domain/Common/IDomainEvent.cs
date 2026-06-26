namespace Cauce.Domain.Common;

/// <summary>
/// Representa un evento de dominio: un hecho relevante ocurrido dentro del
/// dominio que otras partes del sistema pueden necesitar conocer.
/// </summary>
public interface IDomainEvent
{
    /// <summary>
    /// Identificador único del evento.
    /// </summary>
    Guid Id { get; }

    /// <summary>
    /// Momento, en UTC, en que ocurrió el evento.
    /// </summary>
    DateTime OccurredOn { get; }
}
