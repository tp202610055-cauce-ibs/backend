using Cauce.Application.Patients.Dtos;
using Cauce.Domain.Patients.Enums;

namespace Cauce.Application.Patients.UseCases.GetPatientProfile;

/// <summary>
/// Perfil clínico completo del paciente.
/// </summary>
/// <param name="ProfileId">Identificador del perfil.</param>
/// <param name="UserId">Identificador de la cuenta del paciente.</param>
/// <param name="DateOfBirth">Fecha de nacimiento.</param>
/// <param name="BiologicalSex">Sexo biológico.</param>
/// <param name="WeightKg">Peso en kilogramos.</param>
/// <param name="HeightCm">Estatura en centímetros.</param>
/// <param name="Bmi">Índice de masa corporal calculado.</param>
/// <param name="BmiCategory">Categoría del IMC.</param>
/// <param name="Age">Edad cumplida.</param>
/// <param name="IbsSubtype">Subtipo de SII.</param>
/// <param name="DiagnosisDate">Fecha de diagnóstico, si se conoce.</param>
/// <param name="Medications">Medicación actual.</param>
/// <param name="OnboardingCompleted">Indica si el onboarding está completo.</param>
/// <param name="Allergies">Alergias declaradas por el paciente.</param>
/// <param name="AssignedNutritionist">Nutricionista asignado, si lo hay.</param>
public sealed record GetPatientProfileResult(
    Guid ProfileId,
    Guid UserId,
    DateOnly DateOfBirth,
    BiologicalSex BiologicalSex,
    decimal WeightKg,
    decimal HeightCm,
    decimal Bmi,
    string BmiCategory,
    int Age,
    IbsSubtype IbsSubtype,
    DateOnly? DiagnosisDate,
    string? Medications,
    bool OnboardingCompleted,
    IReadOnlyList<PatientAllergySummary> Allergies,
    NutritionistAssignmentSummary? AssignedNutritionist);
