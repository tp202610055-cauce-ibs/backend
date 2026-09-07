using Cauce.Domain.Common;
using Cauce.Domain.Identity.Enums;

namespace Cauce.Domain.Identity.Events;

/// <summary>
/// Evento de dominio emitido cuando un paciente se vincula a un nutricionista consumiendo un código de
/// invitación válido, sea al registrarse o al canjearlo después. Permite notificar al nutricionista del
/// nuevo paciente en su seguimiento (US20 CA01).
/// </summary>
/// <param name="PatientUserId">Identificador de la cuenta del paciente.</param>
/// <param name="NutritionistUserId">Identificador de la cuenta del nutricionista.</param>
/// <param name="InvitationCodeId">Identificador del código de invitación consumido.</param>
/// <param name="OccurredOn">Momento de ocurrencia, en UTC.</param>
/// <param name="Context">
/// Momento del ciclo de vida en que ocurrió la vinculación, que determina el texto del aviso. Su valor
/// por defecto preserva el comportamiento del flujo de registro y mantiene válidas las construcciones
/// de cuatro argumentos, incluidos los eventos ya persistidos en el outbox (acta A43).
/// </param>
public sealed record PatientLinkedToNutritionistEvent(
    Guid PatientUserId,
    Guid NutritionistUserId,
    Guid InvitationCodeId,
    DateTime OccurredOn,
    LinkContext Context = LinkContext.RegistrationLink) : IDomainEvent
{
    /// <summary>
    /// Identificador único del evento.
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();
}
