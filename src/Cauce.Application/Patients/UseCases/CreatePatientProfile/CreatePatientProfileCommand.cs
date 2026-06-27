using Cauce.Domain.Patients.Enums;
using MediatR;

namespace Cauce.Application.Patients.UseCases.CreatePatientProfile;

/// <summary>
/// Comando para crear el perfil clínico del paciente autenticado. Si el paciente
/// usó un código de invitación al registrarse, además se crea la asignación con su
/// nutricionista.
/// </summary>
/// <param name="DateOfBirth">Fecha de nacimiento.</param>
/// <param name="BiologicalSex">Sexo biológico.</param>
/// <param name="WeightKg">Peso en kilogramos.</param>
/// <param name="HeightCm">Estatura en centímetros.</param>
/// <param name="IbsSubtype">Subtipo clínico de SII.</param>
/// <param name="DiagnosisDate">Fecha de diagnóstico, opcional.</param>
/// <param name="Medications">Medicación actual, opcional.</param>
public sealed record CreatePatientProfileCommand(
    DateOnly DateOfBirth,
    BiologicalSex BiologicalSex,
    decimal WeightKg,
    decimal HeightCm,
    IbsSubtype IbsSubtype,
    DateOnly? DiagnosisDate,
    string? Medications) : IRequest<CreatePatientProfileResult>;
