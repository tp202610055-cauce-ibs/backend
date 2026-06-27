using MediatR;

namespace Cauce.Application.Patients.UseCases.GetPatientProfile;

/// <summary>
/// Consulta que devuelve el perfil clínico completo del paciente autenticado,
/// incluyendo IMC, edad, alergias y nutricionista asignado.
/// </summary>
public sealed record GetPatientProfileQuery : IRequest<GetPatientProfileResult>;
