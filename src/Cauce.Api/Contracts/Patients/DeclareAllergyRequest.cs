using Cauce.Domain.Patients.Enums;

namespace Cauce.Api.Contracts.Patients;

/// <summary>
/// Cuerpo de la petición para declarar una alergia del paciente.
/// </summary>
/// <param name="AllergyId">Identificador de la alergia del catálogo.</param>
/// <param name="Severity">Severidad declarada.</param>
/// <param name="Notes">Notas opcionales.</param>
public sealed record DeclareAllergyRequest(
    Guid AllergyId,
    AllergySeverity Severity,
    string? Notes);
