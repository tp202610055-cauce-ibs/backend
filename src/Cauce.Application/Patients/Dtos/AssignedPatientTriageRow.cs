using Cauce.Domain.ClinicalRegistry.Enums;
using Cauce.Domain.Patients.Enums;

namespace Cauce.Application.Patients.Dtos;

/// <summary>
/// Fila cruda del panel de triaje (US18) tal como la produce la consulta con joins y
/// subconsultas del repositorio. Contiene las métricas necesarias para calcular el
/// <see cref="PriorityLevel"/> y el orden en la capa de aplicación. No se expone en la
/// API: el handler la proyecta a <see cref="AssignedPatientSummary"/>.
/// </summary>
/// <param name="PatientUserId">Identificador de la cuenta del paciente.</param>
/// <param name="PatientFullName">Nombre completo del paciente.</param>
/// <param name="AssignmentId">Identificador de la asignación.</param>
/// <param name="AssignedAt">Momento de la asignación, en UTC.</param>
/// <param name="ProfileCompleted">Indica si el paciente completó su onboarding.</param>
/// <param name="IbsSubtype">Subtipo de SII del paciente, si tiene perfil.</param>
/// <param name="LatestIbsSssScore">Puntaje total de la evaluación IBS-SSS más reciente, o <see langword="null"/>.</param>
/// <param name="LatestSeverity">Categoría de severidad de la evaluación más reciente, o <see langword="null"/>.</param>
/// <param name="LastMealAt">Fecha del último registro de comida, o <see langword="null"/>.</param>
/// <param name="LastSymptomAt">Fecha del último registro de síntoma, o <see langword="null"/>.</param>
/// <param name="LastAssessmentAt">Fecha de la última evaluación IBS-SSS, o <see langword="null"/>.</param>
/// <param name="PendingReviewOver24hCount">Cantidad de recomendaciones en revisión pendiente vencidas (más de 24 h).</param>
public sealed record AssignedPatientTriageRow(
    Guid PatientUserId,
    string PatientFullName,
    Guid AssignmentId,
    DateTime AssignedAt,
    bool ProfileCompleted,
    IbsSubtype? IbsSubtype,
    int? LatestIbsSssScore,
    SeverityCategory? LatestSeverity,
    DateTime? LastMealAt,
    DateTime? LastSymptomAt,
    DateTime? LastAssessmentAt,
    int PendingReviewOver24hCount);
