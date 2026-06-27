using Cauce.Domain.Patients.Enums;
using MediatR;

namespace Cauce.Application.Patients.UseCases.UpdatePatientProfile;

/// <summary>
/// Comando para actualizar campos modificables del perfil clínico del paciente
/// autenticado. Un campo en <see langword="null"/> indica que no se modifica. La
/// fecha de nacimiento y el sexo biológico no son editables.
/// </summary>
/// <param name="WeightKg">Nuevo peso en kilogramos, opcional.</param>
/// <param name="HeightCm">Nueva estatura en centímetros, opcional.</param>
/// <param name="IbsSubtype">Nuevo subtipo de SII, opcional.</param>
/// <param name="DiagnosisDate">Nueva fecha de diagnóstico, opcional.</param>
/// <param name="Medications">Nueva medicación, opcional.</param>
public sealed record UpdatePatientProfileCommand(
    decimal? WeightKg,
    decimal? HeightCm,
    IbsSubtype? IbsSubtype,
    DateOnly? DiagnosisDate,
    string? Medications) : IRequest<UpdatePatientProfileResult>;
