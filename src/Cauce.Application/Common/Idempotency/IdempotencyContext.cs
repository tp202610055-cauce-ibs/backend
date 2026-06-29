namespace Cauce.Application.Common.Idempotency;

/// <summary>
/// Implementación con alcance de petición (scoped) de <see cref="IIdempotencyContext"/>.
/// Mantiene el indicador de reintento de la petición actual.
/// </summary>
public sealed class IdempotencyContext : IIdempotencyContext
{
    /// <inheritdoc />
    public bool WasReplay { get; set; }
}
