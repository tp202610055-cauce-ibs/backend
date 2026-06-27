namespace Cauce.Application.Patients.UseCases.DeclarePatientAllergy;

/// <summary>
/// Resultado de la declaración de una alergia.
/// </summary>
/// <param name="PatientAllergyId">Identificador de la declaración creada.</param>
public sealed record DeclarePatientAllergyResult(Guid PatientAllergyId);
