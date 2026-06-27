using Cauce.Domain.Patients.Enums;

namespace Cauce.Api.Contracts.Patients;

/// <summary>
/// Cuerpo de la petición de actualización del perfil clínico. Los campos en
/// <see langword="null"/> no se modifican.
/// </summary>
/// <param name="WeightKg">Nuevo peso en kilogramos, opcional.</param>
/// <param name="HeightCm">Nueva estatura en centímetros, opcional.</param>
/// <param name="IbsSubtype">Nuevo subtipo de SII, opcional.</param>
/// <param name="DiagnosisDate">Nueva fecha de diagnóstico, opcional.</param>
/// <param name="Medications">Nueva medicación, opcional.</param>
public sealed record UpdatePatientProfileRequest(
    decimal? WeightKg,
    decimal? HeightCm,
    IbsSubtype? IbsSubtype,
    DateOnly? DiagnosisDate,
    string? Medications);
