using Cauce.Application.ClinicalRegistry.Dtos;
using MediatR;

namespace Cauce.Application.ClinicalRegistry.UseCases.GetPatientHistoryForNutritionist;

/// <summary>
/// Consulta del diario de un paciente asignado (comidas, síntomas y notas clínicas) para su
/// nutricionista, en un rango de fechas. Requiere asignación activa y queda auditada.
/// </summary>
/// <param name="PatientUserId">Identificador de la cuenta del paciente.</param>
/// <param name="From">Inicio del rango (UTC).</param>
/// <param name="To">Fin del rango (UTC).</param>
public sealed record GetPatientHistoryForNutritionistQuery(Guid PatientUserId, DateTime From, DateTime To)
    : IRequest<IReadOnlyList<HistoryEvent>>;
