using Cauce.Application.Patients.Dtos;
using Cauce.Domain.Patients.Enums;

namespace Cauce.Application.Patients.UseCases.GetAssignedPatientDetail;

/// <summary>
/// Detalle clínico de un paciente asignado, visible para su nutricionista.
/// </summary>
/// <param name="PatientUserId">Identificador de la cuenta del paciente.</param>
/// <param name="FullName">Nombre completo del paciente.</param>
/// <param name="Age">Edad cumplida.</param>
/// <param name="Bmi">Índice de masa corporal calculado.</param>
/// <param name="BmiCategory">Categoría del IMC.</param>
/// <param name="IbsSubtype">Subtipo de SII.</param>
/// <param name="OnboardingCompleted">Indica si el onboarding está completo.</param>
/// <param name="Allergies">Alergias declaradas por el paciente.</param>
public sealed record GetAssignedPatientDetailResult(
    Guid PatientUserId,
    string FullName,
    int Age,
    decimal Bmi,
    string BmiCategory,
    IbsSubtype IbsSubtype,
    bool OnboardingCompleted,
    IReadOnlyList<PatientAllergySummary> Allergies);
