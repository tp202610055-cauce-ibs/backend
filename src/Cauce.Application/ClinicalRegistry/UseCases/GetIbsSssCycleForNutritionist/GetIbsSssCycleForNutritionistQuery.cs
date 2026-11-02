using Cauce.Application.ClinicalRegistry.Dtos;
using MediatR;

namespace Cauce.Application.ClinicalRegistry.UseCases.GetIbsSssCycleForNutritionist;

/// <summary>
/// Consulta del ciclo vigente del cuestionario IBS-SSS de un paciente asignado, para su nutricionista.
/// Requiere asignación activa y queda auditada.
/// </summary>
/// <param name="PatientUserId">Identificador de la cuenta del paciente.</param>
public sealed record GetIbsSssCycleForNutritionistQuery(Guid PatientUserId) : IRequest<IbsSssCycleStatusDto>;
