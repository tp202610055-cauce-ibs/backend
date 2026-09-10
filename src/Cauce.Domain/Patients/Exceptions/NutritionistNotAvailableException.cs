using Cauce.Domain.Common.Exceptions;
using Cauce.Domain.Identity.Enums;

namespace Cauce.Domain.Patients.Exceptions;

/// <summary>
/// Se lanza cuando el nutricionista dueño de un código de invitación no está en condiciones de recibir
/// pacientes al momento del canje (decisión D11 del bloque Backend-Fix-2). Falla cualquier estado que no
/// sea <see cref="UserStatus.Active"/>.
/// </summary>
/// <remarks>
/// Para el cliente es un solo escenario arquitectónico, "el nutricionista no puede atender", con un
/// único <c>errorCode</c>: <c>nutritionist_not_available</c>. El estado exacto viaja en la extensión
/// <c>reason</c> del envelope, para que la app pueda afinar el mensaje si quiere (esperar unas horas
/// ante una cuenta sin activar, contactar al hospital ante una suspendida) o ignorarlo y mostrar uno
/// genérico.
/// <para>
/// El código de invitación <b>no se consume</b> cuando se lanza esta excepción, de modo que el
/// nutricionista pueda reutilizarlo o reemitirlo.
/// </para>
/// </remarks>
public sealed class NutritionistNotAvailableException : DomainException
{
    /// <summary>
    /// Inicializa la excepción con el estado que impide el canje.
    /// </summary>
    /// <param name="status">Estado real de la cuenta del nutricionista.</param>
    public NutritionistNotAvailableException(UserStatus status)
        : base("El nutricionista asociado a este código no está disponible actualmente.")
    {
        Status = status;
    }

    /// <summary>
    /// Estado de la cuenta del nutricionista que impidió el canje. Se registra en la bitácora para
    /// permitir investigar el rechazo después.
    /// </summary>
    public UserStatus Status { get; }

    /// <summary>
    /// Motivo en el formato estable que consume el cliente, para la extensión <c>reason</c> del
    /// envelope de error.
    /// </summary>
    public string Reason => Status switch
    {
        UserStatus.PendingActivation => "pending_activation",
        UserStatus.Inactive => "inactive",
        UserStatus.Suspended => "suspended",
        _ => "unavailable"
    };
}
