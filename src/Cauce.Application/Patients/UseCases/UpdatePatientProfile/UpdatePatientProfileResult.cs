namespace Cauce.Application.Patients.UseCases.UpdatePatientProfile;

/// <summary>
/// Resultado de la actualización del perfil clínico, con los valores recalculados.
/// </summary>
/// <param name="Bmi">Índice de masa corporal recalculado.</param>
/// <param name="BmiCategory">Categoría del IMC.</param>
/// <param name="Age">Edad cumplida del paciente.</param>
public sealed record UpdatePatientProfileResult(
    decimal Bmi,
    string BmiCategory,
    int Age);
