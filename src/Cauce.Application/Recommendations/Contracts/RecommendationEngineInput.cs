namespace Cauce.Application.Recommendations.Contracts;

/// <summary>
/// Entrada del motor de recomendaciones: el contexto del paciente, sus alimentos candidatos
/// y el contexto de la comida.
/// </summary>
/// <param name="Patient">Instantánea del contexto clínico del paciente.</param>
/// <param name="CandidateFoods">Alimentos candidatos a evaluar.</param>
/// <param name="MealContext">Contexto de la comida.</param>
public sealed record RecommendationEngineInput(
    PatientContextSnapshot Patient,
    IReadOnlyList<CandidateFood> CandidateFoods,
    MealContext MealContext);
