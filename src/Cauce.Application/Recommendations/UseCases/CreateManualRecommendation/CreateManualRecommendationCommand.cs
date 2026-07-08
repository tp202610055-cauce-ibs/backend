using Cauce.Domain.Recommendations.Enums;
using MediatR;

namespace Cauce.Application.Recommendations.UseCases.CreateManualRecommendation;

/// <summary>
/// Comando para crear manualmente una recomendación dietética para un paciente asignado (US29). La
/// recomendación queda aprobada de inmediato (<see cref="RecommendationStatus.ManualApproved"/>).
/// </summary>
/// <param name="PatientId">Identificador del paciente destinatario.</param>
/// <param name="Title">Título de la recomendación.</param>
/// <param name="Description">Descripción de la recomendación.</param>
/// <param name="Steps">Pasos accionables, opcional.</param>
/// <param name="ClinicalNote">Nota clínica del nutricionista.</param>
/// <param name="ValidUntil">Fecha de vigencia, o <see langword="null"/> si no caduca.</param>
public sealed record CreateManualRecommendationCommand(
    Guid PatientId,
    string Title,
    string Description,
    IReadOnlyList<string>? Steps,
    string ClinicalNote,
    DateTime? ValidUntil) : IRequest<CreateManualRecommendationResult>;

/// <summary>
/// Resultado de la creación manual de una recomendación.
/// </summary>
/// <param name="RecommendationId">Identificador de la recomendación creada.</param>
/// <param name="Status">Estado de la recomendación.</param>
public sealed record CreateManualRecommendationResult(Guid RecommendationId, RecommendationStatus Status);
