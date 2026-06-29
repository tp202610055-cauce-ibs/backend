using Cauce.Application.ClinicalRegistry.Dtos;
using MediatR;

namespace Cauce.Application.ClinicalRegistry.UseCases.GetLatestIbsSssAssessment;

/// <summary>
/// Consulta de la evaluación IBS-SSS más reciente del paciente autenticado.
/// </summary>
public sealed record GetLatestIbsSssAssessmentQuery : IRequest<IbsSssAssessmentSummary?>;
