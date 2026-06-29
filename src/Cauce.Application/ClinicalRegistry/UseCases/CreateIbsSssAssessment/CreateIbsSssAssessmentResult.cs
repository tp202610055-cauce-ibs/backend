using Cauce.Domain.ClinicalRegistry.Enums;

namespace Cauce.Application.ClinicalRegistry.UseCases.CreateIbsSssAssessment;

/// <summary>
/// Resultado del registro de una evaluación IBS-SSS, con los valores calculados por el
/// servidor.
/// </summary>
/// <param name="AssessmentId">Identificador de la evaluación creada.</param>
/// <param name="TotalScore">Puntaje total calculado (0–500).</param>
/// <param name="SeverityCategory">Categoría de severidad calculada.</param>
/// <param name="NextAssessmentDate">Fecha sugerida de la próxima evaluación.</param>
/// <param name="TriggeredOnboardingCompletion">Indica si la evaluación cerró el onboarding del paciente.</param>
public sealed record CreateIbsSssAssessmentResult(
    Guid AssessmentId,
    int TotalScore,
    SeverityCategory SeverityCategory,
    DateOnly? NextAssessmentDate,
    bool TriggeredOnboardingCompletion);
