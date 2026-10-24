using System.Globalization;
using System.Text.Json;
using Cauce.Domain.Auditing.Enums;
using Cauce.Infrastructure.Qa.Manifest;

namespace Cauce.Infrastructure.Qa.Inspection;

/// <summary>
/// Texto en español de una fila de auditoría para mostrarle al revisor. Del contexto adicional solo salen las claves de
/// una lista blanca; nunca IP, user agent, hashes ni identificadores de otras cuentas.
/// </summary>
public static class QaAuditLogFormatter
{
    private static readonly Dictionary<AuditActionType, string> ActionLabels = new()
    {
        [AuditActionType.Login] = "Ingreso exitoso",
        [AuditActionType.Logout] = "Cierre de sesión",
        [AuditActionType.FailedLogin] = "Ingreso fallido",
        [AuditActionType.AccountLocked] = "Ingreso rechazado por bloqueo",
        [AuditActionType.Create] = "Creación",
        [AuditActionType.Update] = "Modificación",
        [AuditActionType.Delete] = "Eliminación o anonimización",
        [AuditActionType.Approve] = "Aprobación",
        [AuditActionType.Reject] = "Rechazo",
        [AuditActionType.Deliver] = "Entrega",
        [AuditActionType.ExportPdf] = "Reporte PDF",
        [AuditActionType.ViewPatientRecord] = "Consulta de historia clínica",
        [AuditActionType.Register] = "Registro",
        [AuditActionType.PasswordResetRequest] = "Solicitud de recuperación de contraseña",
        [AuditActionType.PasswordResetConfirm] = "Cambio de contraseña por recuperación",
        [AuditActionType.Export] = "Exportación de datos",
        [AuditActionType.LlmFallback] = "Explicación de respaldo",
        [AuditActionType.TokenRefresh] = "Renovación de sesión",
        [AuditActionType.FailedTokenRefresh] = "Renovación de sesión fallida",
        [AuditActionType.VerificationEmailResendRequest] = "Reenvío del correo de verificación",
        [AuditActionType.NutritionistAssignment] = "Canje de código de invitación",
        [AuditActionType.AccountActivation] = "Activación de cuenta",
        [AuditActionType.ActivationEmailResend] = "Reenvío del enlace de activación",
        [AuditActionType.MealAssociationCorrection] = "Corrección de comida asociada",
        [AuditActionType.FailedLogout] = "Cierre de sesión rechazado"
    };

    private static readonly Dictionary<string, string> EntityLabels = new(StringComparer.Ordinal)
    {
        ["User"] = "Cuenta",
        ["Recommendation"] = "Recomendación",
        ["RecommendationFeedback"] = "Retroalimentación",
        ["Meal"] = "Comida",
        ["Symptom"] = "Síntoma",
        ["IbsSssAssessment"] = "Cuestionario IBS-SSS",
        ["PatientProfile"] = "Perfil",
        ["PatientAllergy"] = "Alergia",
        ["NutritionistPatient"] = "Vínculo",
        ["CustomFood"] = "Alimento propio",
        ["ClinicalNote"] = "Nota clínica",
        ["InvitationCode"] = "Código de invitación",
        ["clinical_report"] = "Reporte PDF"
    };

    // Claves del contexto que se pueden mostrar; el resto se omite.
    private static readonly Dictionary<string, string> ContextLabels = new(StringComparer.Ordinal)
    {
        ["channel"] = "canal",
        ["cause"] = "causa",
        ["operation"] = "operación",
        ["archive_reason"] = "motivo",
        ["source"] = "origen",
        ["email"] = "correo",
        ["acknowledged_allergens"] = "alérgenos aceptados",
        ["mealsAccepted"] = "comidas",
        ["symptomsAccepted"] = "síntomas",
        ["counts"] = "conteos",
        ["trigger"] = "disparo",
        ["role"] = "rol",
        ["actor"] = "actor",
        ["outcome"] = "resultado",
        ["range_days"] = "días",
        ["reason"] = "motivo",
        ["error_type"] = "error"
    };

    /// <summary>
    /// Una línea por fila: hora de Lima, hora UTC, acción, entidad, contexto y autor.
    /// </summary>
    public static string FormatLine(QaAuditRow row, QaInspectionScope scope)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(scope);
        var lima = (row.OccurredAt + QaClock.LimaOffset).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        var utc = row.OccurredAt.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        var action = ActionLabels.GetValueOrDefault(row.Action, row.Action.ToString());
        var entity = EntityLabels.GetValueOrDefault(row.EntityType, row.EntityType)
            + (row.EntityId is { } id ? $" #{id.ToString("N")[..8]}" : string.Empty);
        return $"{lima} Lima | {utc} UTC | {action} | {entity} | {FormatContext(row.Context)} | {Author(row, scope)}";
    }

    /// <summary>
    /// Autor de la fila: el correo si es una cuenta del alcance; si no, quién la escribió sin nombrar a otras cuentas.
    /// </summary>
    public static string Author(QaAuditRow row, QaInspectionScope scope)
    {
        ArgumentNullException.ThrowIfNull(row);
        ArgumentNullException.ThrowIfNull(scope);
        return row.Match switch
        {
            QaAuditMatch.UnknownEmail => "correo sin cuenta (no se puede atribuir)",
            QaAuditMatch.MaskedEmail => "sin sesión (coincide el correo enmascarado)",
            _ when row.ActorUserId is null => "Sistema (proceso automático)",
            _ => scope.EmailOf(row.ActorUserId) ?? "otra cuenta"
        };
    }

    /// <summary>
    /// Contexto adicional con las claves permitidas, como <c>clave=valor</c> separados por coma.
    /// </summary>
    public static string FormatContext(string? context)
    {
        if (string.IsNullOrWhiteSpace(context))
        {
            return "-";
        }

        try
        {
            using var document = JsonDocument.Parse(context);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return "(datos omitidos)";
            }

            var parts = new List<string>();
            var omitted = false;
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (ContextLabels.TryGetValue(property.Name, out var label) && Value(property.Name, property.Value) is { } value)
                {
                    parts.Add($"{label}={value}");
                }
                else
                {
                    omitted = true;
                }
            }

            if (omitted)
            {
                parts.Add("(otros datos omitidos)");
            }

            return parts.Count == 0 ? "-" : string.Join(", ", parts);
        }
        catch (JsonException)
        {
            return "(datos omitidos)";
        }
    }

    private static string? Value(string key, JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString(),
        JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => value.GetRawText(),
        JsonValueKind.Array when key == "acknowledged_allergens" => string.Join(
            " y ",
            value.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.Object && item.TryGetProperty("allergen_name", out _))
                .Select(item => item.GetProperty("allergen_name").GetString())),
        JsonValueKind.Object when key == "counts" => string.Join(
            " ",
            value.EnumerateObject().Where(item => item.Value.ValueKind == JsonValueKind.Number).Select(item => $"{item.Name}:{item.Value.GetRawText()}")),
        _ => null
    };
}
