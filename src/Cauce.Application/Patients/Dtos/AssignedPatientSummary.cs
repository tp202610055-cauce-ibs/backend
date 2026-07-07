using Cauce.Domain.Patients.Enums;

namespace Cauce.Application.Patients.Dtos;

/// <summary>
/// Resumen de un paciente asignado a un nutricionista, para el panel de triaje del
/// portal (US18). Incluye las métricas de priorización y el <see cref="PriorityLevel"/>
/// calculado por la capa de aplicación.
/// </summary>
/// <param name="PatientUserId">Identificador de la cuenta del paciente.</param>
/// <param name="PatientFullName">Nombre completo del paciente.</param>
/// <param name="AssignmentId">Identificador de la asignación.</param>
/// <param name="AssignedAt">Momento de la asignación, en UTC.</param>
/// <param name="ProfileCompleted">Indica si el paciente completó su onboarding.</param>
/// <param name="IbsSubtype">Subtipo de SII del paciente, si tiene perfil.</param>
/// <param name="LatestIbsSssScore">Puntaje total de la evaluación IBS-SSS más reciente, o <see langword="null"/>.</param>
/// <param name="LastActivityAt">Fecha de la actividad más reciente del paciente (comida, síntoma o evaluación), o <see langword="null"/>.</param>
/// <param name="PendingReviewOver24hCount">Cantidad de recomendaciones en revisión pendiente vencidas (más de 24 h).</param>
/// <param name="PriorityLevel">Nivel de prioridad de atención calculado para el triaje.</param>
public sealed record AssignedPatientSummary(
    Guid PatientUserId,
    string PatientFullName,
    Guid AssignmentId,
    DateTime AssignedAt,
    bool ProfileCompleted,
    IbsSubtype? IbsSubtype,
    int? LatestIbsSssScore,
    DateTime? LastActivityAt,
    int PendingReviewOver24hCount,
    PriorityLevel PriorityLevel);
