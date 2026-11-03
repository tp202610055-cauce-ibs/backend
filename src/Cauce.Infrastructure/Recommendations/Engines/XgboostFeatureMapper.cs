using Cauce.Application.Recommendations.Contracts;
using Cauce.Domain.ClinicalRegistry.Enums;

namespace Cauce.Infrastructure.Recommendations.Engines;

/// <summary>
/// Convierte el paciente, el alimento y la cantidad en las 10 variables de xgboost-sii-v1.2.0, en el
/// orden del contrato: edad, IMC, tipo de SII, años de diagnóstico, nivel FODMAP, cuatro grupos y gramos.
/// </summary>
/// <remarks>
/// Las categorías se traducen por significado, nunca por el número del enum, porque el orden difiere.
/// Edad e IMC no se recortan al rango del entrenamiento (edad 19 a 77, IMC 18 a 40).
/// </remarks>
public static class XgboostFeatureMapper
{
    /// <summary>
    /// Cantidad de variables de entrada del modelo.
    /// </summary>
    public const int FeatureCount = 10;

    /// <summary>
    /// Años de diagnóstico usados cuando no hay dato: la mediana del entrenamiento.
    /// </summary>
    public const float DefaultYearsSinceDiagnosis = 3f;

    private const float ConstipationSubtype = 0f;
    private const float DiarrheaSubtype = 1f;
    private const float MixedSubtype = 2f;
    private const float UnclassifiedSubtype = 3f;

    private const float HighLevel = 0f;
    private const float LowLevel = 1f;

    private const int OligosGroup = 0;
    private const int FructoseGroup = 1;
    private const int PolyolsGroup = 2;
    private const int LactoseGroup = 3;
    private const int GroupCount = 4;

    /// <summary>
    /// Construye el vector de entrada del modelo.
    /// </summary>
    /// <param name="patient">Instantánea del contexto del paciente.</param>
    /// <param name="food">Alimento candidato.</param>
    /// <param name="quantityGrams">
    /// La misma cantidad que usa el motor de regla. No convierte unidades: una taza registrada vale 1.
    /// </param>
    /// <returns>Las 10 variables, en el orden del contrato del modelo.</returns>
    /// <exception cref="ArgumentNullException">Si el paciente o el alimento son nulos.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Si un número o el nivel FODMAP son inválidos.</exception>
    /// <exception cref="ArgumentException">Si el subtipo o una etiqueta FODMAP son desconocidos.</exception>
    public static float[] Map(PatientContextSnapshot patient, CandidateFood food, float quantityGrams)
    {
        ArgumentNullException.ThrowIfNull(patient);
        ArgumentNullException.ThrowIfNull(food);

        if (patient.AgeYears <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(patient), "La edad del paciente debe ser mayor que cero.");
        }

        if (patient.BodyMassIndex <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(patient), "El IMC del paciente debe ser mayor que cero.");
        }

        if (!float.IsFinite(quantityGrams) || quantityGrams <= 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantityGrams), "La cantidad debe ser un número finito mayor que cero.");
        }

        var (level, groups) = MapFodmap(food);

        return
        [
            patient.AgeYears,
            (float)patient.BodyMassIndex,
            MapSubtype(patient.IbsSubtype),
            ResolveYearsSinceDiagnosis(patient.YearsSinceDiagnosis),
            level,
            groups[OligosGroup],
            groups[FructoseGroup],
            groups[PolyolsGroup],
            groups[LactoseGroup],
            quantityGrams
        ];
    }

    /// <summary>
    /// Devuelve los años de diagnóstico; 0 o nulo significan que no hay dato y usan la mediana del entrenamiento.
    /// </summary>
    /// <param name="yearsSinceDiagnosis">Años desde el diagnóstico, si se conocen.</param>
    /// <returns>Los años que recibe el modelo.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Si el valor es negativo.</exception>
    public static float ResolveYearsSinceDiagnosis(int? yearsSinceDiagnosis)
    {
        if (yearsSinceDiagnosis is < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(yearsSinceDiagnosis), "Los años de diagnóstico no pueden ser negativos.");
        }

        return yearsSinceDiagnosis is null or 0 ? DefaultYearsSinceDiagnosis : yearsSinceDiagnosis.Value;
    }

    private static float MapSubtype(string? subtype)
    {
        if (string.IsNullOrWhiteSpace(subtype))
        {
            return UnclassifiedSubtype;
        }

        return subtype.Trim().ToUpperInvariant() switch
        {
            "SII-C" => ConstipationSubtype,
            "SII-D" => DiarrheaSubtype,
            "SII-M" => MixedSubtype,
            "SII-U" => UnclassifiedSubtype,
            _ => throw new ArgumentException("El subtipo de SII del paciente es desconocido.", nameof(subtype))
        };
    }

    /// <summary>
    /// Traduce el nivel FODMAP y las etiquetas del alimento al nivel y los cuatro grupos del modelo.
    /// </summary>
    /// <remarks>
    /// El modelo solo conoce high y low, y en su dataset el 99,4 % de las filas con algún grupo en 1 o 2
    /// son high (83 840 de 84 336). Por convención, Moderate va como high con sus grupos en 1.
    /// </remarks>
    private static (float Level, float[] Groups) MapFodmap(CandidateFood food)
    {
        var (level, taggedValue) = food.FodmapLevel switch
        {
            FodmapLevel.High => (HighLevel, 2f),
            FodmapLevel.Moderate => (HighLevel, 1f),
            FodmapLevel.Low => (LowLevel, 1f),
            _ => throw new ArgumentOutOfRangeException(
                nameof(food), $"El alimento '{food.Name}' tiene un nivel FODMAP desconocido.")
        };

        var groups = new float[GroupCount];
        if (food.FodmapTags is null)
        {
            return (level, groups);
        }

        foreach (var tag in food.FodmapTags.Split(','))
        {
            groups[MapTag(tag, food.Name)] = taggedValue;
        }

        return (level, groups);
    }

    private static int MapTag(string tag, string foodName)
    {
        return tag.Trim().ToLowerInvariant() switch
        {
            "fructans" or "oligos" => OligosGroup,
            "fructose" => FructoseGroup,
            "polyols" => PolyolsGroup,
            "lactose" => LactoseGroup,
            _ => throw new ArgumentException(
                $"El alimento '{foodName}' tiene una etiqueta FODMAP desconocida: '{tag}'.")
        };
    }
}
