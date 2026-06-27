using Cauce.Domain.Patients.Enums;

namespace Cauce.Application.Patients.Dtos;

/// <summary>
/// Resumen de una alergia declarada por un paciente, incluyendo el nombre del
/// catálogo.
/// </summary>
/// <param name="PatientAllergyId">Identificador de la declaración.</param>
/// <param name="AllergyId">Identificador de la alergia del catálogo.</param>
/// <param name="AllergyName">Nombre de la alergia.</param>
/// <param name="AllergyType">Tipo de reacción.</param>
/// <param name="Severity">Severidad declarada.</param>
/// <param name="Notes">Notas del paciente.</param>
/// <param name="DeclaredAt">Momento de la declaración, en UTC.</param>
public sealed record PatientAllergySummary(
    Guid PatientAllergyId,
    Guid AllergyId,
    string AllergyName,
    AllergyType AllergyType,
    AllergySeverity Severity,
    string? Notes,
    DateTime DeclaredAt);
