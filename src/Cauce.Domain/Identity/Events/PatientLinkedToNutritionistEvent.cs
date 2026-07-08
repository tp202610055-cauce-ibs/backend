using Cauce.Domain.Common;

namespace Cauce.Domain.Identity.Events;

/// <summary>
/// Evento de dominio emitido cuando un paciente se vincula a un nutricionista al registrarse con un
/// código de invitación válido. Permite notificar al nutricionista del nuevo paciente en su seguimiento
/// (US20 CA01).
/// </summary>
/// <param name="PatientUserId">Identificador de la cuenta del paciente.</param>
/// <param name="NutritionistUserId">Identificador de la cuenta del nutricionista.</param>
/// <param name="InvitationCodeId">Identificador del código de invitación consumido.</param>
/// <param name="OccurredOn">Momento de ocurrencia, en UTC.</param>
public sealed record PatientLinkedToNutritionistEvent(
    Guid PatientUserId,
    Guid NutritionistUserId,
    Guid InvitationCodeId,
    DateTime OccurredOn) : IDomainEvent
{
    /// <summary>
    /// Identificador único del evento.
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();
}
