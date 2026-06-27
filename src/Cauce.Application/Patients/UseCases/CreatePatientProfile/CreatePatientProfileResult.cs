namespace Cauce.Application.Patients.UseCases.CreatePatientProfile;

/// <summary>
/// Resultado de la creación del perfil clínico del paciente.
/// </summary>
/// <param name="ProfileId">Identificador del perfil creado.</param>
/// <param name="Bmi">Índice de masa corporal calculado.</param>
/// <param name="BmiCategory">Categoría del IMC.</param>
/// <param name="Age">Edad cumplida del paciente.</param>
/// <param name="NutritionistAssigned">Indica si se creó una asignación con un nutricionista.</param>
/// <param name="NutritionistAssignmentId">Identificador de la asignación creada, si aplica.</param>
public sealed record CreatePatientProfileResult(
    Guid ProfileId,
    decimal Bmi,
    string BmiCategory,
    int Age,
    bool NutritionistAssigned,
    Guid? NutritionistAssignmentId);
