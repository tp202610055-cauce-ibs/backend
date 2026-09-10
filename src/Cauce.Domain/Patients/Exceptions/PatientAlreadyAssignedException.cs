using Cauce.Domain.Common.Exceptions;

namespace Cauce.Domain.Patients.Exceptions;

/// <summary>
/// Se lanza cuando un paciente que ya tiene un nutricionista activo intenta canjear otro código de
/// invitación (decisión D5 del bloque Backend-Fix-2). El canje se rechaza en vez de sobrescribir: un
/// cambio de nutricionista es una decisión clínica, no el efecto de que el paciente pegue un código.
/// </summary>
/// <remarks>
/// Es distinta de <see cref="NutritionistAssignmentAlreadyExistsException"/>, que describe un par
/// nutricionista-paciente ya vinculado. Aquí lo que impide la operación es que el paciente tenga
/// <b>cualquier</b> asignación activa, sea con ese nutricionista o con otro.
/// </remarks>
public sealed class PatientAlreadyAssignedException : DomainException
{
    /// <summary>
    /// Inicializa la excepción con el mensaje estándar.
    /// </summary>
    public PatientAlreadyAssignedException()
        : base("El paciente ya tiene un nutricionista asignado.")
    {
    }
}
