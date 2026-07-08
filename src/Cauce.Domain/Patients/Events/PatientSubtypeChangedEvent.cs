using Cauce.Domain.Common;
using Cauce.Domain.Patients.Enums;

namespace Cauce.Domain.Patients.Events;

/// <summary>
/// Evento de dominio emitido cuando un paciente cambia su subtipo clínico de SII en su perfil. Permite
/// notificar al nutricionista asignado del cambio (US03 CA03).
/// </summary>
/// <param name="UserId">Identificador de la cuenta del paciente.</param>
/// <param name="OldSubtype">Subtipo previo.</param>
/// <param name="NewSubtype">Subtipo nuevo.</param>
/// <param name="OccurredOn">Momento de ocurrencia, en UTC.</param>
public sealed record PatientSubtypeChangedEvent(
    Guid UserId,
    IbsSubtype OldSubtype,
    IbsSubtype NewSubtype,
    DateTime OccurredOn) : IDomainEvent
{
    /// <summary>
    /// Identificador único del evento.
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();
}
