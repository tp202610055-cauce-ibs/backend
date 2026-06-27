using Cauce.Domain.Patients.Enums;

namespace Cauce.Api.Contracts.Patients;

/// <summary>
/// Cuerpo de la petición de creación del perfil clínico del paciente.
/// </summary>
/// <param name="DateOfBirth">Fecha de nacimiento.</param>
/// <param name="BiologicalSex">Sexo biológico.</param>
/// <param name="WeightKg">Peso en kilogramos.</param>
/// <param name="HeightCm">Estatura en centímetros.</param>
/// <param name="IbsSubtype">Subtipo clínico de SII.</param>
/// <param name="DiagnosisDate">Fecha de diagnóstico, opcional.</param>
/// <param name="Medications">Medicación actual, opcional.</param>
public sealed record CreatePatientProfileRequest(
    DateOnly DateOfBirth,
    BiologicalSex BiologicalSex,
    decimal WeightKg,
    decimal HeightCm,
    IbsSubtype IbsSubtype,
    DateOnly? DiagnosisDate,
    string? Medications);
