using Cauce.Domain.ClinicalRegistry.Enums;
using MediatR;

namespace Cauce.Application.ClinicalRegistry.UseCases.CreateIbsSssAssessment;

/// <summary>
/// Comando para registrar una evaluación IBS-SSS del paciente autenticado. El cliente
/// envía únicamente las cinco dimensiones; el servidor calcula el puntaje total y la
/// categoría de severidad.
/// </summary>
/// <param name="AssessmentType">Tipo de evaluación (línea base o periódica).</param>
/// <param name="PainSeverity">Severidad del dolor (0–100).</param>
/// <param name="PainFrequency">Frecuencia del dolor (0–100).</param>
/// <param name="BloatingSeverity">Severidad de la distensión (0–100).</param>
/// <param name="BowelHabitsDissatisfaction">Insatisfacción con el hábito intestinal (0–100).</param>
/// <param name="LifeInterference">Interferencia con la vida diaria (0–100).</param>
public sealed record CreateIbsSssAssessmentCommand(
    AssessmentType AssessmentType,
    int PainSeverity,
    int PainFrequency,
    int BloatingSeverity,
    int BowelHabitsDissatisfaction,
    int LifeInterference) : IRequest<CreateIbsSssAssessmentResult>;
