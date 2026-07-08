using Cauce.Application.ClinicalRegistry.Dtos;
using MediatR;

namespace Cauce.Application.Patients.UseCases.GetPatientEvolutionForNutritionist;

/// <summary>
/// Consulta que devuelve las métricas de evolución clínica de un paciente para el
/// nutricionista asignado (US21). Requiere una asignación activa; de lo contrario
/// responde 403.
/// </summary>
/// <param name="PatientUserId">Identificador de la cuenta del paciente.</param>
public sealed record GetPatientEvolutionForNutritionistQuery(Guid PatientUserId)
    : IRequest<PatientEvolutionForNutritionistResult>;

/// <summary>
/// Métricas de evolución clínica de un paciente para el nutricionista (US21).
/// </summary>
/// <param name="IbsSssTimeline">Serie de evaluaciones IBS-SSS con su diferencia respecto de la línea base.</param>
/// <param name="BaselineScore">Puntaje de la línea base, o <see langword="null"/> si aún no existe.</param>
/// <param name="LatestScore">Puntaje de la evaluación más reciente, o <see langword="null"/>.</param>
/// <param name="PercentChangeFromBaseline">Variación porcentual del puntaje respecto de la línea base (negativa indica mejoría), o <see langword="null"/>.</param>
/// <param name="SignificantClinicalResponse">Indica si hay respuesta clínica significativa (reducción de al menos 50 puntos respecto de la línea base).</param>
/// <param name="RegistrationFrequency14d">Cantidad de registros clínicos (comidas y síntomas) en los últimos 14 días.</param>
public sealed record PatientEvolutionForNutritionistResult(
    IReadOnlyList<IbsSssEvolutionEntry> IbsSssTimeline,
    int? BaselineScore,
    int? LatestScore,
    decimal? PercentChangeFromBaseline,
    bool SignificantClinicalResponse,
    int RegistrationFrequency14d);
