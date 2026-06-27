using Cauce.Application.Patients.Dtos;
using MediatR;

namespace Cauce.Application.Patients.UseCases.ListAssignedPatients;

/// <summary>
/// Consulta que devuelve los pacientes activos asignados al nutricionista
/// autenticado, ordenados por fecha de asignación descendente.
/// </summary>
public sealed record ListAssignedPatientsQuery : IRequest<IReadOnlyList<AssignedPatientSummary>>;
