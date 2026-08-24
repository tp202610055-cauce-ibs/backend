using System.Diagnostics;
using System.Text.Json;
using Cauce.Application.Common.Exceptions;
using Cauce.Domain.ClinicalRegistry.Exceptions;
using Cauce.Domain.Common.Exceptions;
using Cauce.Domain.Identity.Exceptions;
using Cauce.Domain.Notifications.Exceptions;
using Cauce.Domain.Patients.Exceptions;
using Cauce.Domain.Recommendations.Exceptions;
using Cauce.Domain.Reports.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Cauce.Api.Middleware;

/// <summary>
/// Middleware que captura las excepciones no controladas del pipeline, las
/// registra sin exponer datos personales (PII) y devuelve una respuesta de error
/// estandarizada según RFC 7807 (<c>application/problem+json</c>) con un código de
/// error legible por máquina en la extensión <c>errorCode</c>.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private const string ProblemJsonContentType = "application/problem+json";

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    /// <summary>
    /// Inicializa el middleware con el siguiente delegado del pipeline y el logger.
    /// </summary>
    /// <param name="next">Siguiente delegado en el pipeline de peticiones.</param>
    /// <param name="logger">Logger de la categoría del middleware.</param>
    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>
    /// Ejecuta el middleware: invoca el resto del pipeline y traduce cualquier
    /// excepción a una respuesta de problema.
    /// </summary>
    /// <param name="context">Contexto HTTP de la petición.</param>
    /// <returns>Tarea que representa la operación asíncrona.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await HandleExceptionAsync(context, exception).ConfigureAwait(false);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;

        ProblemDetails problemDetails;
        if (IsAuditLogTamperAttempt(exception))
        {
            // TS04 CA02: un intento de modificar audit_logs (bloqueado por el trigger de inmutabilidad)
            // se registra como evento de seguridad de nivel Error, con actor, IP y tabla, para su
            // detección posterior (acta A18).
            LogAuditTamperAttempt(context, exception);
            problemDetails = BuildProblem(
                StatusCodes.Status403Forbidden,
                "Operación no permitida",
                "La bitácora de auditoría es inmutable y no admite modificaciones.",
                "audit_log_immutable",
                traceId);
        }
        else if (exception is UnconfirmedAllergensException allergenException)
        {
            // US10 CA03: coincidencias de alérgenos sin confirmar. Se devuelve 409 con el detalle de las
            // coincidencias para que el cliente pida confirmación al paciente.
            _logger.LogWarning(
                "Handled exception of type {ExceptionType} mapped to {StatusCode}. TraceId: {TraceId}",
                nameof(UnconfirmedAllergensException), StatusCodes.Status409Conflict, traceId);
            problemDetails = BuildProblem(
                StatusCodes.Status409Conflict,
                "Alérgenos detectados",
                allergenException.Message,
                "unconfirmed_allergens",
                traceId);
            problemDetails.Extensions["detected"] = true;
            problemDetails.Extensions["allergens"] = allergenException.DetectedAllergens;
        }
        else
        {
            problemDetails = exception is ValidationException validationException
                ? BuildValidationProblem(validationException, traceId)
                : BuildProblemForException(exception, traceId);

            if (exception is AccountLockedException lockedException)
            {
                // US05 CA02 exige informar al usuario cuánto debe esperar, no solo que está bloqueado.
                problemDetails.Extensions["lockedUntil"] = lockedException.LockedUntil;
            }

            if (problemDetails.Status == StatusCodes.Status500InternalServerError)
            {
                _logger.LogError(
                    exception,
                    "Unhandled exception of type {ExceptionType}. TraceId: {TraceId}",
                    exception.GetType().Name,
                    traceId);
            }
            else
            {
                _logger.LogWarning(
                    "Handled exception of type {ExceptionType} mapped to {StatusCode}. TraceId: {TraceId}",
                    exception.GetType().Name,
                    problemDetails.Status,
                    traceId);
            }
        }

        context.Response.Clear();
        context.Response.StatusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;
        context.Response.ContentType = ProblemJsonContentType;
        await context.Response.WriteAsJsonAsync(problemDetails, problemDetails.GetType()).ConfigureAwait(false);
    }

    private static bool IsAuditLogTamperAttempt(Exception exception)
    {
        var postgres = exception as PostgresException
            ?? (exception as DbUpdateException)?.InnerException as PostgresException;

        return postgres is not null
            && postgres.SqlState == PostgresErrorCodes.CheckViolation
            && postgres.MessageText.Contains("audit_logs", StringComparison.OrdinalIgnoreCase);
    }

    private void LogAuditTamperAttempt(HttpContext context, Exception exception)
    {
        var actor = context.User?.FindFirst("sub")?.Value
            ?? context.User?.Identity?.Name
            ?? "anonymous";
        var ip = context.Connection?.RemoteIpAddress?.ToString() ?? "unknown";

        _logger.LogError(
            exception,
            "SecurityEvent audit_tamper_attempt: intento de modificar {Table} por actor {Actor} desde {Ip} (event_type={EventType}).",
            "audit_logs",
            actor,
            ip,
            "audit_tamper_attempt");
    }

    private static ProblemDetails BuildProblemForException(Exception exception, string traceId)
    {
        var (status, title, errorCode, detail) = exception switch
        {
            DuplicateEmailException => (
                StatusCodes.Status409Conflict, "Correo duplicado", "duplicate_email", exception.Message),
            InvalidInvitationCodeException => (
                StatusCodes.Status400BadRequest, "Código de invitación inválido", "invalid_invitation_code", exception.Message),
            ExpiredInvitationCodeException => (
                StatusCodes.Status400BadRequest, "Código de invitación expirado", "expired_invitation_code", exception.Message),
            InvitationCodeAlreadyUsedException => (
                StatusCodes.Status400BadRequest, "Código de invitación ya usado", "invitation_code_already_used", exception.Message),
            AccountLockedException => (
                StatusCodes.Status423Locked, "Cuenta bloqueada", "account_locked", exception.Message),
            InvalidPasswordResetTokenException => (
                StatusCodes.Status400BadRequest, "Token de restablecimiento inválido", "invalid_password_reset_token", exception.Message),
            ExpiredPasswordResetTokenException => (
                StatusCodes.Status400BadRequest, "Token de restablecimiento expirado", "expired_password_reset_token", exception.Message),
            ConsentTextMismatchException => (
                StatusCodes.Status400BadRequest, "Consentimiento no coincide", "consent_text_mismatch", exception.Message),
            ActivePilotRetentionException => (
                StatusCodes.Status409Conflict, "Retención por piloto activo", "active_pilot_retention", exception.Message),
            ConsentRecordNotFoundException => (
                StatusCodes.Status404NotFound, "Consentimiento no encontrado", "consent_record_not_found", exception.Message),
            DuplicatePatientProfileException => (
                StatusCodes.Status409Conflict, "Perfil de paciente duplicado", "duplicate_patient_profile", exception.Message),
            PatientProfileNotFoundException => (
                StatusCodes.Status404NotFound, "Perfil de paciente no encontrado", "patient_profile_not_found", exception.Message),
            InvalidBiometricValueException => (
                StatusCodes.Status400BadRequest, "Valor biométrico inválido", "invalid_biometric_value", exception.Message),
            DuplicatePatientAllergyException => (
                StatusCodes.Status409Conflict, "Alergia duplicada", "duplicate_patient_allergy", exception.Message),
            AllergyNotFoundException => (
                StatusCodes.Status404NotFound, "Alergia no encontrada", "allergy_not_found", exception.Message),
            NutritionistAssignmentAlreadyExistsException => (
                StatusCodes.Status409Conflict, "Asignación ya existente", "nutritionist_assignment_exists", exception.Message),
            OnboardingAlreadyCompletedException => (
                StatusCodes.Status409Conflict, "Onboarding ya completado", "onboarding_already_completed", exception.Message),
            PatientAccessNotAuthorizedException => (
                StatusCodes.Status403Forbidden, "Acceso al paciente no autorizado", "unauthorized_patient_access", exception.Message),
            FoodItemNotFoundException => (
                StatusCodes.Status404NotFound, "Alimento no encontrado", "food_item_not_found", exception.Message),
            CustomFoodNotFoundException => (
                StatusCodes.Status404NotFound, "Alimento personalizado no encontrado", "custom_food_not_found", exception.Message),
            DuplicateCustomFoodException => (
                StatusCodes.Status409Conflict, "Alimento personalizado duplicado", "duplicate_custom_food", exception.Message),
            CustomFoodInUseException => (
                StatusCodes.Status409Conflict, "Alimento personalizado en uso", "custom_food_in_use", exception.Message),
            DuplicateIngredientException => (
                StatusCodes.Status409Conflict, "Ingrediente duplicado", "duplicate_ingredient", exception.Message),
            IngredientNotFoundException => (
                StatusCodes.Status404NotFound, "Ingrediente no encontrado", "ingredient_not_found", exception.Message),
            MealNotFoundException => (
                StatusCodes.Status404NotFound, "Comida no encontrada", "meal_not_found", exception.Message),
            SymptomNotFoundException => (
                StatusCodes.Status404NotFound, "Síntoma no encontrado", "symptom_not_found", exception.Message),
            ClinicalNoteNotFoundException => (
                StatusCodes.Status404NotFound, "Nota clínica no encontrada", "clinical_note_not_found", exception.Message),
            InvalidClinicalNoteAssociationException => (
                StatusCodes.Status400BadRequest, "Asociación de nota clínica inválida", "invalid_clinical_note_association", exception.Message),
            DuplicateBaselineAssessmentException => (
                StatusCodes.Status409Conflict, "Evaluación de línea base duplicada", "duplicate_baseline_assessment", exception.Message),
            InvalidIbsSssDimensionException => (
                StatusCodes.Status400BadRequest, "Dimensión IBS-SSS inválida", "invalid_ibs_sss_dimension", exception.Message),
            InvalidMealRegistrationException => (
                StatusCodes.Status400BadRequest, "Registro de comida inválido", "invalid_meal_registration", exception.Message),
            PatientResourceAccessException => (
                StatusCodes.Status403Forbidden, "Acceso a recurso no autorizado", "patient_resource_access_denied", exception.Message),
            IdempotencyMismatchException => (
                StatusCodes.Status409Conflict, "Conflicto de idempotencia", "idempotency_mismatch", exception.Message),
            RecommendationNotFoundException => (
                StatusCodes.Status404NotFound, "Recomendación no encontrada", "recommendation_not_found", exception.Message),
            RecommendationAccessDeniedException => (
                StatusCodes.Status403Forbidden, "Acceso a la recomendación no autorizado", "recommendation_access_denied",
                "No tiene autorización para acceder a esta recomendación."),
            InsufficientClinicalHistoryException => (
                StatusCodes.Status422UnprocessableEntity, "Historial clínico insuficiente", "insufficient_clinical_history", exception.Message),
            AllCandidatesFilteredByAllergiesException => (
                StatusCodes.Status422UnprocessableEntity, "Candidatos filtrados por alergias", "all_candidates_filtered_by_allergies", exception.Message),
            NoActiveModelVersionException => (
                StatusCodes.Status422UnprocessableEntity, "Sin versión de modelo activa", "no_active_model_version", exception.Message),
            RecommendationExpiredException => (
                StatusCodes.Status409Conflict, "Recomendación expirada", "recommendation_expired", exception.Message),
            InvalidRecommendationStateTransitionException => (
                StatusCodes.Status409Conflict, "Transición de estado inválida", "conflict_state", exception.Message),
            RecommendationNotArchivableException => (
                StatusCodes.Status409Conflict, "Recomendación no archivable", "recommendation_not_archivable", exception.Message),
            InvalidCredentialsException => (
                StatusCodes.Status401Unauthorized, "Credenciales inválidas", "invalid_credentials", exception.Message),
            InvalidRefreshTokenException => (
                StatusCodes.Status401Unauthorized, "Token de refresco inválido", "invalid_refresh_token", exception.Message),
            // Va antes del caso general de DomainException, del que hereda. Es inconsistencia entre
            // Keycloak y la base local, no un error del cliente: 500 con detalle genérico.
            UserLocalMissingException => (
                StatusCodes.Status500InternalServerError, "Inconsistencia de identidad", "user_local_missing",
                "Ocurrió un error al resolver la identidad del usuario."),
            ReportAccessDeniedException => (
                StatusCodes.Status403Forbidden, "Acceso al reporte no autorizado", "report_access_denied", exception.Message),
            PatientHasNoDataInPeriodException => (
                StatusCodes.Status422UnprocessableEntity, "Sin datos en el período", "patient_has_no_data_in_period", exception.Message),
            ReportPeriodInvalidException => (
                StatusCodes.Status422UnprocessableEntity, "Período de reporte inválido", "report_period_invalid", exception.Message),
            InvalidNotificationStateTransitionException => (
                StatusCodes.Status500InternalServerError, "Transición de notificación inválida", "invalid_notification_state_transition",
                "Ocurrió un error al procesar una notificación."),
            NotificationDeliveryFailedException => (
                StatusCodes.Status500InternalServerError, "Fallo de entrega de notificación", "notification_delivery_failed",
                "Ocurrió un error al entregar una notificación."),
            DomainException => (
                StatusCodes.Status400BadRequest, "Regla de dominio violada", "domain_rule_violation", exception.Message),
            KeycloakIntegrationException => (
                StatusCodes.Status502BadGateway, "Error del proveedor de identidad", "keycloak_integration_error",
                "No se pudo completar la operación con el proveedor de identidad."),
            UnauthorizedAccessException => (
                StatusCodes.Status403Forbidden, "Acceso denegado", "forbidden",
                "No tiene permisos para realizar esta operación."),
            KeyNotFoundException => (
                StatusCodes.Status404NotFound, "Recurso no encontrado", "not_found",
                "El recurso solicitado no existe."),
            _ => (
                StatusCodes.Status500InternalServerError, "Error interno del servidor", "internal_server_error",
                "Ocurrió un error inesperado al procesar la solicitud.")
        };

        return BuildProblem(status, title, detail, errorCode, traceId);
    }

    private static ProblemDetails BuildValidationProblem(ValidationException exception, string traceId)
    {
        // Las claves de este diccionario no las transforma la política de nombres de JSON: solo se
        // aplica a nombres de propiedad, y DictionaryKeyPolicy no está configurado. Se normalizan
        // aquí para que el contrato salga íntegramente en camelCase.
        var errors = exception.Errors
            .GroupBy(failure => ToCamelCase(failure.PropertyName))
            .ToDictionary(
                group => group.Key,
                group => group.Select(failure => failure.ErrorMessage).ToArray());

        var problem = new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Error de validación",
            Detail = "Una o más reglas de validación no se cumplieron."
        };
        problem.Extensions["traceId"] = traceId;
        problem.Extensions["errorCode"] = "validation_error";
        return problem;
    }

    /// <summary>
    /// Convierte a camelCase el nombre de propiedad que reporta FluentValidation, respetando las
    /// rutas anidadas y los indexadores de colección que generan <c>RuleForEach</c> y
    /// <c>SetValidator</c>. Por ejemplo, <c>Items[0].Quantity</c> se convierte en
    /// <c>items[0].quantity</c>.
    /// </summary>
    /// <param name="propertyName">Nombre de propiedad tal como lo reporta FluentValidation.</param>
    /// <returns>El nombre en camelCase, o el mismo valor si viene nulo o vacío.</returns>
    private static string ToCamelCase(string propertyName)
    {
        if (string.IsNullOrEmpty(propertyName))
        {
            return propertyName;
        }

        var segments = propertyName.Split('.');
        for (var i = 0; i < segments.Length; i++)
        {
            segments[i] = ConvertSegment(segments[i]);
        }

        return string.Join('.', segments);

        // Un segmento puede traer un indexador (por ejemplo, "Items[0]"); solo se convierte la parte
        // del identificador y se conserva el indexador intacto.
        static string ConvertSegment(string segment)
        {
            var indexerStart = segment.IndexOf('[', StringComparison.Ordinal);
            if (indexerStart < 0)
            {
                return JsonNamingPolicy.CamelCase.ConvertName(segment);
            }

            var identifier = segment[..indexerStart];
            var indexer = segment[indexerStart..];
            return JsonNamingPolicy.CamelCase.ConvertName(identifier) + indexer;
        }
    }

    private static ProblemDetails BuildProblem(int status, string title, string detail, string errorCode, string traceId)
    {
        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail
        };
        problem.Extensions["traceId"] = traceId;
        problem.Extensions["errorCode"] = errorCode;
        return problem;
    }
}

/// <summary>
/// Métodos de extensión para registrar <see cref="ExceptionHandlingMiddleware"/>
/// en el pipeline de la aplicación.
/// </summary>
public static class ExceptionHandlingMiddlewareExtensions
{
    /// <summary>
    /// Añade el middleware de manejo de excepciones al pipeline de peticiones.
    /// </summary>
    /// <param name="app">Constructor del pipeline de la aplicación.</param>
    /// <returns>El mismo constructor para encadenamiento.</returns>
    public static IApplicationBuilder UseExceptionHandlingMiddleware(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ExceptionHandlingMiddleware>();
    }
}
