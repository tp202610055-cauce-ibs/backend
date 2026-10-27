using Cauce.Domain.ClinicalRegistry.Enums;
using Cauce.Domain.Patients.Enums;
using Cauce.Domain.Recommendations.Enums;

namespace Cauce.Infrastructure.Reports;

/// <summary>
/// Textos en español de los valores que el reporte recibe como nombres de enumeración. Un valor
/// desconocido se devuelve tal cual.
/// </summary>
internal static class ReportLabels
{
    public static string IbsSubtype(string value) => Parse<IbsSubtype>(value) switch
    {
        Domain.Patients.Enums.IbsSubtype.IbsD => "SII-D (diarrea)",
        Domain.Patients.Enums.IbsSubtype.IbsC => "SII-C (estreñimiento)",
        Domain.Patients.Enums.IbsSubtype.IbsM => "SII-M (mixto)",
        Domain.Patients.Enums.IbsSubtype.IbsU => "SII-U (no clasificado)",
        _ => value
    };

    public static string AllergySeverity(string value) => Parse<AllergySeverity>(value) switch
    {
        Domain.Patients.Enums.AllergySeverity.Mild => "leve",
        Domain.Patients.Enums.AllergySeverity.Moderate => "moderada",
        Domain.Patients.Enums.AllergySeverity.Severe => "severa",
        _ => value
    };

    public static string MealTime(string value) => Parse<MealTime>(value) switch
    {
        Domain.ClinicalRegistry.Enums.MealTime.Breakfast => "Desayuno",
        Domain.ClinicalRegistry.Enums.MealTime.Lunch => "Almuerzo",
        Domain.ClinicalRegistry.Enums.MealTime.Dinner => "Cena",
        Domain.ClinicalRegistry.Enums.MealTime.Snack => "Refrigerio",
        _ => value
    };

    public static string SymptomType(string value) => Parse<SymptomType>(value) switch
    {
        Domain.ClinicalRegistry.Enums.SymptomType.AbdominalPain => "Dolor abdominal",
        Domain.ClinicalRegistry.Enums.SymptomType.Bloating => "Distensión abdominal",
        Domain.ClinicalRegistry.Enums.SymptomType.Flatulence => "Flatulencia",
        Domain.ClinicalRegistry.Enums.SymptomType.Diarrhea => "Diarrea",
        Domain.ClinicalRegistry.Enums.SymptomType.Constipation => "Estreñimiento",
        Domain.ClinicalRegistry.Enums.SymptomType.Nausea => "Náuseas",
        Domain.ClinicalRegistry.Enums.SymptomType.Reflux => "Reflujo",
        Domain.ClinicalRegistry.Enums.SymptomType.Urgency => "Urgencia",
        Domain.ClinicalRegistry.Enums.SymptomType.Other => "Otro",
        _ => value
    };

    public static string IbsSssCategory(string value) => Parse<SeverityCategory>(value) switch
    {
        SeverityCategory.Mild => "leve",
        SeverityCategory.Moderate => "moderado",
        SeverityCategory.Severe => "severo",
        _ => value
    };

    public static string FeedbackOutcome(string value) => Parse<FeedbackOutcome>(value) switch
    {
        Domain.Recommendations.Enums.FeedbackOutcome.Improvement => "mejoría",
        Domain.Recommendations.Enums.FeedbackOutcome.NoChange => "sin cambios",
        Domain.Recommendations.Enums.FeedbackOutcome.Worsening => "empeoramiento",
        _ => value
    };

    public static string Action(string value) => Parse<ActionType>(value) switch
    {
        ActionType.Suggest => "Incorporar",
        ActionType.Reduce => "Reducir",
        ActionType.Avoid => "Evitar",
        ActionType.Substitute => "Sustituir",
        _ => value
    };

    /// <summary>Orden de presentación de una acción; las desconocidas van al final.</summary>
    public static int ActionOrder(string value) =>
        Enum.TryParse<ActionType>(value, out var action) ? (int)action : int.MaxValue;

    private static TEnum? Parse<TEnum>(string value) where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(value, ignoreCase: false, out var parsed) && Enum.IsDefined(parsed) ? parsed : null;
}
