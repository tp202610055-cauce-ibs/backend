namespace Cauce.Api.Contracts.Recommendations;

/// <summary>
/// Cuerpo de la petición de creación manual de una recomendación (US29).
/// </summary>
/// <param name="PatientId">Identificador del paciente destinatario.</param>
/// <param name="Title">Título de la recomendación.</param>
/// <param name="Description">Descripción de la recomendación.</param>
/// <param name="Steps">Pasos accionables, opcional.</param>
/// <param name="ClinicalNote">Nota clínica del nutricionista.</param>
/// <param name="ValidUntil">Fecha de vigencia, o <see langword="null"/> si no caduca.</param>
public sealed record CreateManualRecommendationRequest(
    Guid PatientId,
    string Title,
    string Description,
    IReadOnlyList<string>? Steps,
    string ClinicalNote,
    DateTime? ValidUntil);
