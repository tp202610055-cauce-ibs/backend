using Cauce.Domain.Patients.Enums;

namespace Cauce.Application.Patients.Dtos;

/// <summary>
/// Resumen de un paciente asignado a un nutricionista, para el listado del portal.
/// </summary>
/// <param name="PatientUserId">Identificador de la cuenta del paciente.</param>
/// <param name="PatientFullName">Nombre completo del paciente.</param>
/// <param name="AssignmentId">Identificador de la asignación.</param>
/// <param name="AssignedAt">Momento de la asignación, en UTC.</param>
/// <param name="ProfileCompleted">Indica si el paciente completó su onboarding.</param>
/// <param name="IbsSubtype">Subtipo de SII del paciente, si tiene perfil.</param>
public sealed record AssignedPatientSummary(
    Guid PatientUserId,
    string PatientFullName,
    Guid AssignmentId,
    DateTime AssignedAt,
    bool ProfileCompleted,
    IbsSubtype? IbsSubtype);
