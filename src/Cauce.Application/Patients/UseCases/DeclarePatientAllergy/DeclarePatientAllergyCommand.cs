using Cauce.Domain.Patients.Enums;
using MediatR;

namespace Cauce.Application.Patients.UseCases.DeclarePatientAllergy;

/// <summary>
/// Comando para que el paciente autenticado declare una alergia del catálogo.
/// </summary>
/// <param name="AllergyId">Identificador de la alergia del catálogo.</param>
/// <param name="Severity">Severidad declarada.</param>
/// <param name="Notes">Notas opcionales.</param>
public sealed record DeclarePatientAllergyCommand(
    Guid AllergyId,
    AllergySeverity Severity,
    string? Notes) : IRequest<DeclarePatientAllergyResult>;
