using Cauce.Domain.Identity;
using Cauce.Domain.Identity.Enums;

namespace Cauce.Application.Common.Interfaces.Patients;

/// <summary>
/// Encapsula la vinculación de un paciente con su nutricionista mediante un código de invitación
/// (acta A41).
/// </summary>
/// <remarks>
/// La vinculación ocurre en <b>dos pasos separados en el tiempo</b>, y por eso el servicio expone dos
/// operaciones en vez de una:
/// <list type="number">
/// <item>
/// El consumo del código, al registrarse o al canjearlo después, que marca el código como usado y
/// publica el evento de vinculación.
/// </item>
/// <item>
/// El establecimiento de la asignación, que crea la fila de <c>nutritionist_patient</c> y hoy ocurre al
/// crear el perfil clínico.
/// </item>
/// </list>
/// Fundirlas en un solo método obligaría al registro a crear la asignación antes de que exista el
/// perfil, que es un cambio de comportamiento observable. El canje post-registro sí invoca ambas.
/// <para>
/// Ninguna de las dos persiste: las escrituras se enrolan en el <c>ChangeTracker</c> y las confirma el
/// <c>SaveChangesAsync</c> del llamador.
/// </para>
/// </remarks>
public interface IPatientNutritionistAssignmentService
{
    /// <summary>
    /// Consume un código de invitación en nombre de un paciente: lo marca como usado y publica el
    /// evento que notifica al nutricionista.
    /// </summary>
    /// <param name="patientId">Identificador de la cuenta del paciente.</param>
    /// <param name="invitation">Código de invitación ya validado por el llamador.</param>
    /// <param name="utcNow">Momento de la operación, en UTC.</param>
    /// <param name="context">
    /// Momento del ciclo de vida en que ocurre la vinculación. Por defecto, el registro inicial.
    /// </param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Tarea que representa la operación asíncrona.</returns>
    Task ConsumeInvitationAsync(
        Guid patientId,
        InvitationCode invitation,
        DateTime utcNow,
        LinkContext context = LinkContext.RegistrationLink,
        CancellationToken ct = default);

    /// <summary>
    /// Establece la asignación entre el paciente y el nutricionista dueño del código que el paciente
    /// consumió. Es idempotente: no crea una segunda asignación si ya hay una activa.
    /// </summary>
    /// <param name="patientId">Identificador de la cuenta del paciente.</param>
    /// <param name="utcNow">Momento de la operación, en UTC.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>
    /// Si se creó la asignación y, en ese caso, su identificador. Devuelve <c>(false, null)</c> cuando
    /// el paciente no consumió ningún código o cuando ya tenía una asignación activa.
    /// </returns>
    Task<(bool Assigned, Guid? AssignmentId)> EstablishAssignmentAsync(
        Guid patientId,
        DateTime utcNow,
        CancellationToken ct = default);
}
