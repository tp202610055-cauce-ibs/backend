using Cauce.Application.Patients.Dtos;
using MediatR;

namespace Cauce.Application.Patients.UseCases.GetNutritionistAssignmentForPatient;

/// <summary>
/// Consulta interna que devuelve la asignación activa de un paciente con su
/// nutricionista, o <see langword="null"/> si no la tiene. No se expone como endpoint;
/// la usan otros handlers que necesitan validar la relación.
/// </summary>
/// <param name="PatientUserId">Identificador de la cuenta del paciente.</param>
public sealed record GetNutritionistAssignmentForPatientQuery(Guid PatientUserId) : IRequest<NutritionistAssignmentSummary?>;
