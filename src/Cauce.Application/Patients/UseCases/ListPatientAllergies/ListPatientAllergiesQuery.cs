using Cauce.Application.Patients.Dtos;
using MediatR;

namespace Cauce.Application.Patients.UseCases.ListPatientAllergies;

/// <summary>
/// Consulta que devuelve las alergias declaradas por el paciente autenticado.
/// </summary>
public sealed record ListPatientAllergiesQuery : IRequest<IReadOnlyList<PatientAllergySummary>>;
