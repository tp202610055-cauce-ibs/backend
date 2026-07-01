namespace Cauce.Application.Recommendations.Contracts;

/// <summary>
/// Instantánea del contexto clínico del paciente que alimenta al motor de recomendaciones
/// y al orquestador de explicaciones. Algunos campos (actividad física, tabaquismo, consumo
/// de alcohol) no tienen origen en el modelo actual de <c>PatientProfile</c> y se completan
/// con valores neutros por defecto; se conservan para el vector de entrada del futuro modelo
/// ONNX (ver "Deuda técnica conocida" en CLAUDE.md).
/// </summary>
/// <param name="AgeYears">Edad cumplida del paciente.</param>
/// <param name="Sex">Sexo del paciente ("Masculino" | "Femenino" | "Otro").</param>
/// <param name="BodyMassIndex">Índice de masa corporal.</param>
/// <param name="IbsSubtype">Subtipo de SII ("SII-C" | "SII-D" | "SII-M" | "SII-U").</param>
/// <param name="YearsSinceDiagnosis">Años transcurridos desde el diagnóstico.</param>
/// <param name="PhysicalActivity">Nivel de actividad física ("Baja" | "Media" | "Alta").</param>
/// <param name="IsSmoker">Indica si el paciente fuma.</param>
/// <param name="ConsumesAlcohol">Indica si el paciente consume alcohol.</param>
public sealed record PatientContextSnapshot(
    int AgeYears,
    string Sex,
    decimal BodyMassIndex,
    string IbsSubtype,
    int YearsSinceDiagnosis,
    string PhysicalActivity,
    bool IsSmoker,
    bool ConsumesAlcohol);
