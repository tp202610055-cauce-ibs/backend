using MediatR;

namespace Cauce.Application.Patients.UseCases.GetAssignedPatientDetail;

/// <summary>
/// Consulta que devuelve el detalle clínico de un paciente asignado, validando que
/// el nutricionista autenticado tenga una asignación activa con él.
/// </summary>
/// <param name="PatientUserId">Identificador de la cuenta del paciente.</param>
public sealed record GetAssignedPatientDetailQuery(Guid PatientUserId) : IRequest<GetAssignedPatientDetailResult>;
