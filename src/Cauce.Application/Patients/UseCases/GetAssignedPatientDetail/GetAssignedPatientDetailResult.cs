using Cauce.Application.Patients.Dtos;
using Cauce.Domain.Patients.Enums;

namespace Cauce.Application.Patients.UseCases.GetAssignedPatientDetail;

/// <summary>
/// Detalle clínico de un paciente asignado, visible para su nutricionista. Si el paciente todavía no
/// completó su perfil, los campos clínicos llegan en <see langword="null"/> y
/// <see cref="OnboardingCompleted"/> en <see langword="false"/>: un paciente recién vinculado aparece en el
/// panel antes de completar el onboarding, y abrir su detalle no debe fallar (acta A69). No lleva el
/// código de paciente, que el acta A59 deja fuera de las vistas de atención.
/// </summary>
/// <param name="PatientUserId">Identificador de la cuenta del paciente.</param>
/// <param name="FullName">Nombre completo del paciente.</param>
/// <param name="Age">Edad cumplida, o <see langword="null"/> sin perfil.</param>
/// <param name="Bmi">Índice de masa corporal calculado, o <see langword="null"/> sin perfil.</param>
/// <param name="BmiCategory">Categoría del IMC, o <see langword="null"/> sin perfil.</param>
/// <param name="IbsSubtype">Subtipo de SII, o <see langword="null"/> sin perfil.</param>
/// <param name="OnboardingCompleted">Indica si el onboarding está completo.</param>
/// <param name="Allergies">Alergias declaradas por el paciente; vacía si no declaró ninguna.</param>
/// <param name="BiologicalSex">Sexo biológico, o <see langword="null"/> sin perfil.</param>
/// <param name="DiagnosisDate">Fecha del diagnóstico de SII, o <see langword="null"/> si no la informó.</param>
/// <param name="Medications">Medicación declarada, o <see langword="null"/> si no la informó.</param>
public sealed record GetAssignedPatientDetailResult(
    Guid PatientUserId,
    string FullName,
    int? Age,
    decimal? Bmi,
    string? BmiCategory,
    IbsSubtype? IbsSubtype,
    bool OnboardingCompleted,
    IReadOnlyList<PatientAllergySummary> Allergies,
    BiologicalSex? BiologicalSex,
    DateOnly? DiagnosisDate,
    string? Medications);
