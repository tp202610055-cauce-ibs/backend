namespace Cauce.Application.Recommendations.Contracts;

/// <summary>
/// Instantánea del contexto clínico del paciente que alimenta al motor de recomendaciones
/// y al orquestador de explicaciones. Actividad física, tabaquismo y consumo de alcohol no tienen
/// origen en <c>PatientProfile</c> y llevan valores neutros por defecto. El modelo real
/// xgboost-sii-v1.2.0 no los usa; solo el modelo ficticio de 5 variables lee los dos últimos.
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
