namespace Cauce.Application.Common.Idempotency;

/// <summary>
/// Contexto de idempotencia con alcance de petición (scoped). El
/// <see cref="Behaviors.IdempotencyBehavior{TRequest,TResponse}"/> lo actualiza para
/// indicar si la respuesta provino de un reintento (replay) de un comando ya procesado,
/// de modo que el controlador pueda devolver 200 en lugar de 201.
/// </summary>
public interface IIdempotencyContext
{
    /// <summary>
    /// Indica si la respuesta del comando idempotente más reciente fue un reintento de
    /// uno ya procesado (con carga idéntica).
    /// </summary>
    bool WasReplay { get; set; }
}
