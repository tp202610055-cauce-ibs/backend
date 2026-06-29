namespace Cauce.Application.Common.Idempotency;

/// <summary>
/// Marca un comando como idempotente respecto del <see cref="ClientGuid"/> generado en
/// el dispositivo. El <see cref="Behaviors.IdempotencyBehavior{TRequest,TResponse}"/>
/// intercepta los comandos que implementan esta interfaz.
/// </summary>
public interface IIdempotentCommand
{
    /// <summary>
    /// Identificador estable del registro generado en el dispositivo (UUID v4). Actúa
    /// como clave de idempotencia.
    /// </summary>
    Guid ClientGuid { get; }
}
