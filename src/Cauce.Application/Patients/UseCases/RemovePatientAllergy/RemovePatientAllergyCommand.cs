using MediatR;

namespace Cauce.Application.Patients.UseCases.RemovePatientAllergy;

/// <summary>
/// Comando para que el paciente autenticado elimine una de sus declaraciones de
/// alergia.
/// </summary>
/// <param name="PatientAllergyId">Identificador de la declaración a eliminar.</param>
public sealed record RemovePatientAllergyCommand(Guid PatientAllergyId) : IRequest;
