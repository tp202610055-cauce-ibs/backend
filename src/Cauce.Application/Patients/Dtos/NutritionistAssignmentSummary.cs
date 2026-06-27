namespace Cauce.Application.Patients.Dtos;

/// <summary>
/// Resumen de la asignación activa de un paciente con su nutricionista.
/// </summary>
/// <param name="AssignmentId">Identificador de la asignación.</param>
/// <param name="NutritionistUserId">Identificador de la cuenta del nutricionista.</param>
/// <param name="NutritionistFullName">Nombre completo del nutricionista.</param>
/// <param name="AssignedAt">Momento de la asignación, en UTC.</param>
public sealed record NutritionistAssignmentSummary(
    Guid AssignmentId,
    Guid NutritionistUserId,
    string NutritionistFullName,
    DateTime AssignedAt);
