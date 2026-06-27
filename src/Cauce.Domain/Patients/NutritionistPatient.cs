using Cauce.Domain.Common;
using Cauce.Domain.Patients.Enums;

namespace Cauce.Domain.Patients;

/// <summary>
/// Asignación entre un nutricionista y un paciente. Es raíz de agregado. Una
/// asignación no se elimina: se cierra con <see cref="Deactivate"/> cuando termina,
/// preservando la trazabilidad.
/// </summary>
public sealed class NutritionistPatient : Entity, IAggregateRoot
{
    /// <summary>
    /// Identificador del nutricionista (cuenta de usuario).
    /// </summary>
    public Guid NutritionistId { get; private set; }

    /// <summary>
    /// Identificador del paciente (cuenta de usuario).
    /// </summary>
    public Guid PatientId { get; private set; }

    /// <summary>
    /// Identificador del código de invitación que originó la asignación, si aplica.
    /// </summary>
    public Guid? InvitationCodeId { get; private set; }

    /// <summary>
    /// Estado de la asignación.
    /// </summary>
    public AssignmentStatus Status { get; private set; }

    /// <summary>
    /// Momento de creación de la asignación, en UTC.
    /// </summary>
    public DateTime AssignedAt { get; private set; }

    /// <summary>
    /// Momento de finalización de la asignación, en UTC; <see langword="null"/> si
    /// sigue activa.
    /// </summary>
    public DateTime? UnassignedAt { get; private set; }

    private NutritionistPatient()
    {
    }

    private NutritionistPatient(Guid id, Guid nutritionistId, Guid patientId, Guid? invitationCodeId, DateTime utcNow)
        : base(id)
    {
        NutritionistId = nutritionistId;
        PatientId = patientId;
        InvitationCodeId = invitationCodeId;
        Status = AssignmentStatus.Active;
        AssignedAt = utcNow;
    }

    /// <summary>
    /// Establece una nueva asignación activa.
    /// </summary>
    /// <param name="id">Identificador de la asignación.</param>
    /// <param name="nutritionistId">Identificador del nutricionista.</param>
    /// <param name="patientId">Identificador del paciente.</param>
    /// <param name="invitationCodeId">Identificador del código de invitación, opcional.</param>
    /// <param name="utcNow">Marca de tiempo UTC de creación.</param>
    /// <returns>La nueva asignación.</returns>
    /// <exception cref="ArgumentException">Si los identificadores son inválidos.</exception>
    public static NutritionistPatient Establish(Guid id, Guid nutritionistId, Guid patientId, Guid? invitationCodeId, DateTime utcNow)
    {
        if (nutritionistId == Guid.Empty)
        {
            throw new ArgumentException("El identificador del nutricionista es obligatorio.", nameof(nutritionistId));
        }

        if (patientId == Guid.Empty)
        {
            throw new ArgumentException("El identificador del paciente es obligatorio.", nameof(patientId));
        }

        return new NutritionistPatient(id, nutritionistId, patientId, invitationCodeId, utcNow);
    }

    /// <summary>
    /// Finaliza la asignación.
    /// </summary>
    /// <param name="utcNow">Marca de tiempo UTC de finalización.</param>
    /// <exception cref="InvalidOperationException">Si la asignación ya está inactiva.</exception>
    public void Deactivate(DateTime utcNow)
    {
        if (Status == AssignmentStatus.Inactive)
        {
            throw new InvalidOperationException("La asignación ya está inactiva.");
        }

        Status = AssignmentStatus.Inactive;
        UnassignedAt = utcNow;
    }

    /// <summary>
    /// Reactiva la asignación.
    /// </summary>
    /// <param name="utcNow">Marca de tiempo UTC de reactivación.</param>
    /// <exception cref="InvalidOperationException">Si la asignación ya está activa.</exception>
    public void Reactivate(DateTime utcNow)
    {
        if (Status == AssignmentStatus.Active)
        {
            throw new InvalidOperationException("La asignación ya está activa.");
        }

        Status = AssignmentStatus.Active;
        UnassignedAt = null;
    }

    /// <summary>
    /// Indica si la asignación está activa.
    /// </summary>
    /// <returns><see langword="true"/> si está activa.</returns>
    public bool IsActive()
    {
        return Status == AssignmentStatus.Active;
    }
}
