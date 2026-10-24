using System.Globalization;
using System.Text.Json;

namespace Cauce.Infrastructure.Qa.Manifest;

/// <summary>
/// Lee el manifiesto de QA desde JSON. Junta todos los errores de forma y falla con la lista completa.
/// </summary>
public static class QaManifestLoader
{
    /// <summary>
    /// Versiones del esquema que este código sabe sembrar.
    /// </summary>
    public static readonly IReadOnlyList<string> SupportedVersions = ["1.1-draft2", "1.1"];

    /// <summary>
    /// Lee y valida el manifiesto de un archivo.
    /// </summary>
    /// <exception cref="QaManifestException">Si el archivo no existe o el manifiesto no es válido.</exception>
    public static QaManifest LoadFile(string path)
    {
        if (!File.Exists(path))
        {
            throw new QaManifestException([$"No existe el manifiesto en la ruta {path}."]);
        }

        return Parse(File.ReadAllText(path));
    }

    /// <summary>
    /// Lee y valida el manifiesto a partir de su texto JSON.
    /// </summary>
    /// <exception cref="QaManifestException">Si el JSON no es válido o el manifiesto tiene errores.</exception>
    public static QaManifest Parse(string json)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            throw new QaManifestException([$"El manifiesto no es un JSON válido: {exception.Message}"]);
        }

        using (document)
        {
            var errors = new List<string>();
            var root = document.RootElement;

            var version = OptionalString(root, "manifestVersion");
            if (version is null || !SupportedVersions.Contains(version, StringComparer.Ordinal))
            {
                throw new QaManifestException(
                    [$"Versión de manifiesto desconocida: «{version ?? "(vacía)"}». Se admiten: {string.Join(", ", SupportedVersions)}."]);
            }

            var accounts = ReadArray(root, "accounts", errors, "manifiesto")
                .Select((element, index) => ReadAccount(element, $"accounts[{index}]", errors))
                .OfType<QaAccount>()
                .ToList();
            var cases = ReadArray(root, "cases", errors, "manifiesto")
                .Select((element, index) => ReadCase(element, index, errors))
                .OfType<QaCase>()
                .ToList();

            var manifest = new QaManifest(version, accounts, cases);
            errors.AddRange(QaManifestValidator.Validate(manifest));

            if (errors.Count > 0)
            {
                throw new QaManifestException(errors);
            }

            return manifest;
        }
    }

    private static QaAccount? ReadAccount(JsonElement element, string where, List<string> errors)
    {
        var email = RequiredString(element, "email", where, errors);
        var disposition = OptionalString(element, "disposition") switch
        {
            "seed" => QaDisposition.Seed,
            "mustNotExist" => QaDisposition.MustNotExist,
            var other => Fail<QaDisposition>(errors, $"{where}: disposición desconocida «{other}».")
        };

        QaRole? role = null;
        QaAccountStatus? status = null;
        if (disposition == QaDisposition.Seed)
        {
            role = ParseRole(OptionalString(element, "role"), where, errors);
            status = ParseStatus(OptionalString(element, "status"), where, errors);
        }

        return email is null
            ? null
            : new QaAccount(
                email,
                role,
                disposition,
                status,
                OptionalString(element, "displayName"),
                StringList(element, "usedBy", where, errors));
    }

    private static QaCase? ReadCase(JsonElement element, int index, List<string> errors)
    {
        var cp = RequiredString(element, "cp", $"cases[{index}]", errors);
        if (cp is null)
        {
            return null;
        }

        var seeds = new List<QaSeed>();
        if (element.TryGetProperty("seed", out var seedArray) && seedArray.ValueKind == JsonValueKind.Array)
        {
            var position = 0;
            foreach (var seedElement in seedArray.EnumerateArray())
            {
                var seed = ReadSeed(seedElement, $"{cp} seed[{position}]", errors);
                if (seed is not null)
                {
                    seeds.Add(seed);
                }

                position++;
            }
        }

        return new QaCase(
            cp,
            StringList(element, "accounts", cp, errors),
            StringList(element, "mustNotExist", cp, errors),
            OptionalBool(element, "consumed") ?? false,
            OptionalString(element, "clock"),
            seeds);
    }

    private static QaSeed? ReadSeed(JsonElement element, string where, List<string> errors)
    {
        var type = OptionalString(element, "t");
        switch (type)
        {
            case "note":
                return new QaNoteSeed(OptionalString(element, "text") ?? string.Empty);
            case "account":
                return RequiredString(element, "email", where, errors) is { } email
                    && ParseRole(OptionalString(element, "role"), where, errors) is { } role
                    && ParseStatus(OptionalString(element, "status"), where, errors) is { } status
                        ? new QaAccountSeed(email, role, status)
                        : null;
            case "profile":
                return RequiredString(element, "patient", where, errors) is { } profilePatient
                    ? new QaProfileSeed(
                        profilePatient,
                        OptionalBool(element, "complete") ?? false,
                        OptionalDecimal(element, "weightKg"),
                        OptionalDecimal(element, "heightCm"),
                        OptionalString(element, "subtype"),
                        StringList(element, "allergies", where, errors))
                    : null;
            case "link":
                return RequiredString(element, "patient", where, errors) is { } linkPatient
                    && RequiredString(element, "nutritionist", where, errors) is { } linkNutritionist
                        ? new QaLinkSeed(linkPatient, linkNutritionist, OptionalOffset(element, "since", where, errors))
                        : null;
            case "meal":
                return RequiredString(element, "patient", where, errors) is { } mealPatient
                    && RequiredString(element, "food", where, errors) is { } food
                    && RequiredOffset(element, "ago", where, errors) is { } mealAgo
                        ? new QaMealSeed(mealPatient, food, mealAgo, OptionalDecimal(element, "grams"), OptionalString(element, "slot"))
                        : null;
            case "customFood":
                return RequiredString(element, "patient", where, errors) is { } customPatient
                    && RequiredString(element, "name", where, errors) is { } name
                        ? new QaCustomFoodSeed(customPatient, name, StringList(element, "ingredients", where, errors))
                        : null;
            case "ibs":
                return ReadIbs(element, where, errors);
            case "ibsDue":
                return RequiredString(element, "patient", where, errors) is { } duePatient ? new QaIbsDueSeed(duePatient) : null;
            case "ibsInvitation":
                return RequiredString(element, "patient", where, errors) is { } invitationPatient
                    && RequiredOffset(element, "sentAgo", where, errors) is { } sentAgo
                        ? new QaIbsInvitationSeed(invitationPatient, sentAgo, OptionalBool(element, "answered") ?? false)
                        : null;
            case "symptom":
                return RequiredString(element, "patient", where, errors) is { } symptomPatient
                    && RequiredString(element, "name", where, errors) is { } symptomName
                    && RequiredOffset(element, "ago", where, errors) is { } symptomAgo
                        ? new QaSymptomSeed(symptomPatient, symptomName, OptionalInt(element, "intensity"), symptomAgo)
                        : null;
            case "rec":
                return ReadRecommendation(element, where, errors);
            case "inviteCode":
                return ReadInviteCode(element, where, errors);
            case "activity":
                return RequiredString(element, "patient", where, errors) is { } activityPatient
                    ? new QaActivitySeed(
                        activityPatient,
                        OptionalInt(element, "daysWithRecordsLast14"),
                        OptionalInt(element, "daysWithRecordsInMonth"),
                        OptionalString(element, "month"))
                    : null;
            default:
                errors.Add($"{where}: tipo de siembra desconocido «{type ?? "(vacío)"}».");
                return null;
        }
    }

    private static QaIbsSeed? ReadIbs(JsonElement element, string where, List<string> errors)
    {
        var patient = RequiredString(element, "patient", where, errors);
        var kind = OptionalString(element, "kind");
        if (kind is not ("baseline" or "periodic"))
        {
            errors.Add($"{where}: tipo de cuestionario desconocido «{kind}».");
            return null;
        }

        var score = OptionalInt(element, "score");
        if (score is null or < 0 or > 500)
        {
            errors.Add($"{where}: el puntaje IBS-SSS debe estar entre 0 y 500.");
            return null;
        }

        var ago = RequiredOffset(element, "ago", where, errors);
        return patient is null || ago is null ? null : new QaIbsSeed(patient, kind == "baseline", score.Value, ago);
    }

    private static QaRecommendationSeed? ReadRecommendation(JsonElement element, string where, List<string> errors)
    {
        var patient = RequiredString(element, "patient", where, errors);
        var title = RequiredString(element, "title", where, errors);
        QaRecommendationStatus? status = OptionalString(element, "status") switch
        {
            "pendingReview" => QaRecommendationStatus.PendingReview,
            "approvedAsGenerated" => QaRecommendationStatus.ApprovedAsGenerated,
            "approvedModified" => QaRecommendationStatus.ApprovedModified,
            "createdByNutritionist" => QaRecommendationStatus.CreatedByNutritionist,
            "rejected" => QaRecommendationStatus.Rejected,
            "expired" => QaRecommendationStatus.Expired,
            var other => Fail<QaRecommendationStatus?>(errors, $"{where}: estado de recomendación desconocido «{other}».")
        };

        QaSupportingData? supportingData = null;
        if (element.TryGetProperty("supportingData", out var supporting) && supporting.ValueKind == JsonValueKind.Object)
        {
            supportingData = new QaSupportingData(
                OptionalInt(supporting, "painRecords") ?? 0,
                OptionalInt(supporting, "windowDays") ?? 14);
        }

        if (patient is null || title is null || status is null)
        {
            return null;
        }

        return new QaRecommendationSeed(
            patient,
            title,
            status.Value,
            OptionalString(element, "explanation"),
            OptionalString(element, "explanationSource"),
            OptionalString(element, "confidence"),
            StringList(element, "suggests", where, errors),
            StringList(element, "reduce", where, errors),
            StringList(element, "avoid", where, errors),
            OptionalInt(element, "expiresInDays"),
            OptionalBool(element, "patientOpened") ?? false,
            OptionalBool(element, "patientResponded") ?? false,
            OptionalString(element, "clinicalNote"),
            OptionalString(element, "description"),
            OptionalInt(element, "stepsCount"),
            StringList(element, "steps", where, errors),
            OptionalOffset(element, "receivedAgo", where, errors),
            OptionalOffset(element, "pendingSinceAgo", where, errors),
            OptionalString(element, "approvedInMonth"),
            OptionalInt(element, "validDaysLeft"),
            supportingData,
            OptionalBool(element, "withRecentHistory") ?? false);
    }

    private static QaInviteCodeSeed? ReadInviteCode(JsonElement element, string where, List<string> errors)
    {
        var nutritionist = RequiredString(element, "nutritionist", where, errors);
        QaInviteCodeState? state = OptionalString(element, "state") switch
        {
            "valid" => QaInviteCodeState.Valid,
            "expired" => QaInviteCodeState.Expired,
            "used" => QaInviteCodeState.Used,
            var other => Fail<QaInviteCodeState?>(errors, $"{where}: estado de código desconocido «{other}».")
        };

        return nutritionist is null || state is null
            ? null
            : new QaInviteCodeSeed(nutritionist, state.Value, OptionalInt(element, "ageDays"), OptionalInt(element, "validHoursLeft"));
    }

    private static QaRole? ParseRole(string? value, string where, List<string> errors) => value switch
    {
        "patient" => QaRole.Patient,
        "nutritionist" => QaRole.Nutritionist,
        _ => Fail<QaRole?>(errors, $"{where}: rol desconocido «{value}».")
    };

    private static QaAccountStatus? ParseStatus(string? value, string where, List<string> errors) => value switch
    {
        "active" => QaAccountStatus.Active,
        "disabled" => QaAccountStatus.Disabled,
        "pendingActivation" => QaAccountStatus.PendingActivation,
        _ => Fail<QaAccountStatus?>(errors, $"{where}: estado de cuenta desconocido «{value}».")
    };

    private static T Fail<T>(List<string> errors, string message)
    {
        errors.Add(message);
        return default!;
    }

    private static IEnumerable<JsonElement> ReadArray(JsonElement element, string name, List<string> errors, string where)
    {
        if (element.TryGetProperty(name, out var array) && array.ValueKind == JsonValueKind.Array)
        {
            return array.EnumerateArray().ToList();
        }

        errors.Add($"{where}: falta la lista «{name}».");
        return [];
    }

    private static string? RequiredString(JsonElement element, string name, string where, List<string> errors)
    {
        var value = OptionalString(element, name);
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add($"{where}: falta «{name}».");
            return null;
        }

        return value;
    }

    private static string? OptionalString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static bool? OptionalBool(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : null;

    private static int? OptionalInt(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)
            ? number
            : null;

    private static decimal? OptionalDecimal(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetDecimal() : null;

    private static IReadOnlyList<string> StringList(JsonElement element, string name, string where, List<string> errors)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return [];
        }

        if (value.ValueKind != JsonValueKind.Array || value.EnumerateArray().Any(item => item.ValueKind != JsonValueKind.String))
        {
            errors.Add($"{where}: «{name}» debe ser una lista de textos.");
            return [];
        }

        return value.EnumerateArray().Select(item => item.GetString()!).ToList();
    }

    private static QaOffset? RequiredOffset(JsonElement element, string name, string where, List<string> errors)
    {
        if (!element.TryGetProperty(name, out _))
        {
            errors.Add($"{where}: falta «{name}».");
            return null;
        }

        return OptionalOffset(element, name, where, errors);
    }

    private static QaOffset? OptionalOffset(JsonElement element, string name, string where, List<string> errors)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Object)
        {
            var hours = OptionalInt(value, "hours");
            var days = OptionalInt(value, "days");
            var today = OptionalBool(value, "today") ?? false;
            DateOnly? date = null;
            if (OptionalString(value, "date") is { } text)
            {
                if (DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                {
                    date = parsed;
                }
                else
                {
                    errors.Add($"{where}: «{name}.date» no tiene el formato aaaa-mm-dd.");
                    return null;
                }
            }

            var given = (hours is null ? 0 : 1) + (days is null ? 0 : 1) + (today ? 1 : 0) + (date is null ? 0 : 1);
            if (given == 1 && hours is not < 0 && days is not < 0)
            {
                return new QaOffset(hours, days, today, date);
            }
        }

        errors.Add($"{where}: «{name}» debe tener exactamente uno de hours, days, today o date, sin negativos.");
        return null;
    }
}
