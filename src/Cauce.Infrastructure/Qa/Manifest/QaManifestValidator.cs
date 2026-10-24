using System.Globalization;
using Cauce.Domain.ClinicalRegistry.Enums;
using Cauce.Domain.Patients.Enums;

namespace Cauce.Infrastructure.Qa.Manifest;

/// <summary>
/// Reglas de coherencia del manifiesto que no dependen del catálogo: referencias entre cuentas y casos, roles y valores.
/// </summary>
public static class QaManifestValidator
{
    private static readonly IReadOnlyDictionary<string, IbsSubtype> Subtypes =
        new Dictionary<string, IbsSubtype>(StringComparer.OrdinalIgnoreCase)
        {
            ["SII-D"] = IbsSubtype.IbsD,
            ["SII-E"] = IbsSubtype.IbsC,
            ["SII-C"] = IbsSubtype.IbsC,
            ["SII-M"] = IbsSubtype.IbsM,
            ["SII-U"] = IbsSubtype.IbsU
        };

    private static readonly IReadOnlyDictionary<string, SymptomType> Symptoms =
        new Dictionary<string, SymptomType>(StringComparer.OrdinalIgnoreCase)
        {
            ["Dolor abdominal"] = SymptomType.AbdominalPain,
            ["Distensión abdominal"] = SymptomType.Bloating,
            ["Hinchazón"] = SymptomType.Bloating,
            ["Gases"] = SymptomType.Flatulence,
            ["Flatulencia"] = SymptomType.Flatulence,
            ["Diarrea"] = SymptomType.Diarrhea,
            ["Estreñimiento"] = SymptomType.Constipation,
            ["Náuseas"] = SymptomType.Nausea,
            ["Reflujo"] = SymptomType.Reflux,
            ["Urgencia"] = SymptomType.Urgency,
            ["Otro"] = SymptomType.Other
        };

    private static readonly string[] ExplanationSources = ["LlmGenerated", "Fallback"];
    private static readonly string[] ConfidenceLevels = ["high", "medium", "low"];

    /// <summary>
    /// Traduce el subtipo del manifiesto (<c>SII-D</c>) al del dominio.
    /// </summary>
    public static bool TryParseSubtype(string value, out IbsSubtype subtype) => Subtypes.TryGetValue(value, out subtype);

    /// <summary>
    /// Traduce la etiqueta en español de un síntoma al tipo del dominio.
    /// </summary>
    public static bool TryParseSymptom(string value, out SymptomType type) => Symptoms.TryGetValue(value, out type);

    /// <summary>
    /// Devuelve todos los errores de coherencia; una lista vacía si el manifiesto es coherente.
    /// </summary>
    public static IReadOnlyList<string> Validate(QaManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var errors = new List<string>();

        foreach (var duplicate in manifest.Accounts.GroupBy(account => account.Email, StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1))
        {
            errors.Add($"Cuenta repetida: {duplicate.Key}.");
        }

        foreach (var duplicate in manifest.Cases.GroupBy(testCase => testCase.Cp, StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1))
        {
            errors.Add($"Caso repetido: {duplicate.Key}.");
        }

        foreach (var testCase in manifest.Cases)
        {
            ValidateCase(manifest, testCase, errors);
        }

        return errors;
    }

    private static void ValidateCase(QaManifest manifest, QaCase testCase, List<string> errors)
    {
        foreach (var email in testCase.Accounts)
        {
            RequireSeeded(manifest, testCase.Cp, email, role: null, errors);
        }

        foreach (var email in testCase.MustNotExist)
        {
            if (manifest.FindAccount(email) is not { Disposition: QaDisposition.MustNotExist })
            {
                errors.Add($"{testCase.Cp}: {email} figura como «no debe existir» pero no está así en la lista de cuentas.");
            }
        }

        foreach (var seed in testCase.Seeds)
        {
            ValidateSeed(manifest, testCase.Cp, seed, errors);
        }
    }

    private static void ValidateSeed(QaManifest manifest, string cp, QaSeed seed, List<string> errors)
    {
        switch (seed)
        {
            case QaAccountSeed account:
                var listed = manifest.FindAccount(account.Email);
                if (listed is null || listed.Disposition != QaDisposition.Seed || listed.Role != account.Role || listed.Status != account.Status)
                {
                    errors.Add($"{cp}: la cuenta {account.Email} no coincide con la lista de cuentas (rol o estado).");
                }

                break;
            case QaProfileSeed profile:
                RequireSeeded(manifest, cp, profile.Patient, QaRole.Patient, errors);
                if (profile.Subtype is not null && !Subtypes.ContainsKey(profile.Subtype))
                {
                    errors.Add($"{cp}: subtipo desconocido «{profile.Subtype}».");
                }

                break;
            case QaLinkSeed link:
                RequireSeeded(manifest, cp, link.Patient, QaRole.Patient, errors);
                RequireSeeded(manifest, cp, link.Nutritionist, QaRole.Nutritionist, errors);
                break;
            case QaSymptomSeed symptom:
                RequireSeeded(manifest, cp, symptom.Patient, QaRole.Patient, errors);
                if (!Symptoms.ContainsKey(symptom.Name))
                {
                    errors.Add($"{cp}: síntoma desconocido «{symptom.Name}».");
                }

                if (symptom.Intensity is < 1 or > 100)
                {
                    errors.Add($"{cp}: la intensidad del síntoma debe estar entre 1 y 100.");
                }

                break;
            case QaRecommendationSeed recommendation:
                RequireSeeded(manifest, cp, recommendation.Patient, QaRole.Patient, errors);
                ValidateRecommendation(cp, recommendation, errors);
                break;
            case QaInviteCodeSeed invite:
                RequireSeeded(manifest, cp, invite.Nutritionist, QaRole.Nutritionist, errors);
                break;
            case QaActivitySeed activity:
                RequireSeeded(manifest, cp, activity.Patient, QaRole.Patient, errors);
                if ((activity.DaysWithRecordsLast14 is null) == (activity.DaysWithRecordsInMonth is null))
                {
                    errors.Add($"{cp}: la actividad lleva daysWithRecordsLast14 o daysWithRecordsInMonth, uno de los dos.");
                }
                else if (activity.DaysWithRecordsLast14 is < 1 or > 14)
                {
                    errors.Add($"{cp}: daysWithRecordsLast14 debe estar entre 1 y 14.");
                }
                else if (activity.DaysWithRecordsInMonth is not null && !TryParseMonth(activity.Month, out _))
                {
                    errors.Add($"{cp}: la actividad mensual necesita «month» con el formato aaaa-mm.");
                }

                break;
            case QaNoteSeed:
                break;
            default:
                foreach (var email in seed.ReferencedEmails())
                {
                    RequireSeeded(manifest, cp, email, QaRole.Patient, errors);
                }

                break;
        }
    }

    private static void ValidateRecommendation(string cp, QaRecommendationSeed recommendation, List<string> errors)
    {
        if (recommendation.ExplanationSource is not null && !ExplanationSources.Contains(recommendation.ExplanationSource, StringComparer.Ordinal))
        {
            errors.Add($"{cp}: origen de explicación desconocido «{recommendation.ExplanationSource}».");
        }

        if (recommendation.Confidence is not null && !ConfidenceLevels.Contains(recommendation.Confidence, StringComparer.Ordinal))
        {
            errors.Add($"{cp}: nivel de confianza desconocido «{recommendation.Confidence}».");
        }

        if (recommendation.ApprovedInMonth is not null && !TryParseMonth(recommendation.ApprovedInMonth, out _))
        {
            errors.Add($"{cp}: «approvedInMonth» debe tener el formato aaaa-mm.");
        }
    }

    private static void RequireSeeded(QaManifest manifest, string cp, string email, QaRole? role, List<string> errors)
    {
        var account = manifest.FindAccount(email);
        if (account is null || account.Disposition != QaDisposition.Seed)
        {
            errors.Add($"{cp}: {email} no es una cuenta sembrada.");
        }
        else if (role is not null && account.Role != role)
        {
            errors.Add($"{cp}: {email} debería tener el rol {role}.");
        }
    }

    /// <summary>
    /// Lee un mes con el formato <c>aaaa-mm</c>; devuelve su primer día.
    /// </summary>
    public static bool TryParseMonth(string? value, out DateOnly firstDay) =>
        DateOnly.TryParseExact($"{value}-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out firstDay);
}
