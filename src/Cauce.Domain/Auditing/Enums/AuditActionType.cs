namespace Cauce.Domain.Auditing.Enums;

/// <summary>
/// Tipo de acción registrada en la bitácora de auditoría. En base de datos se
/// persiste como <c>varchar</c> en snake_case lowercase (por ejemplo,
/// <c>"failed_login"</c>) mediante conversión explícita en la configuración EF Core.
/// </summary>
public enum AuditActionType
{
    /// <summary>
    /// Inicio de sesión exitoso.
    /// </summary>
    Login,

    /// <summary>
    /// Cierre de sesión.
    /// </summary>
    Logout,

    /// <summary>
    /// Intento de inicio de sesión fallido.
    /// </summary>
    FailedLogin,

    /// <summary>
    /// Bloqueo de cuenta por superar el número de intentos permitidos.
    /// </summary>
    AccountLocked,

    /// <summary>
    /// Creación de un registro en una tabla auditada.
    /// </summary>
    Create,

    /// <summary>
    /// Modificación de un registro en una tabla auditada.
    /// </summary>
    Update,

    /// <summary>
    /// Eliminación o anonimización de un registro en una tabla auditada.
    /// </summary>
    Delete,

    /// <summary>
    /// Aprobación de una recomendación en el flujo de revisión humana (HITL).
    /// </summary>
    Approve,

    /// <summary>
    /// Rechazo de una recomendación en el flujo de revisión humana (HITL).
    /// </summary>
    Reject,

    /// <summary>
    /// Entrega de una recomendación aprobada al paciente.
    /// </summary>
    Deliver,

    /// <summary>
    /// Exportación de un reporte clínico en formato PDF.
    /// </summary>
    ExportPdf,

    /// <summary>
    /// Acceso a la historia clínica de un paciente desde el portal del nutricionista.
    /// </summary>
    ViewPatientRecord,

    /// <summary>
    /// Registro de una nueva cuenta (paciente o nutricionista) en el sistema.
    /// </summary>
    Register,

    /// <summary>
    /// Solicitud de restablecimiento de contraseña.
    /// </summary>
    PasswordResetRequest,

    /// <summary>
    /// Confirmación de restablecimiento de contraseña.
    /// </summary>
    PasswordResetConfirm,

    /// <summary>
    /// Exportación de la portabilidad de datos del paciente (US25, Ley N° 29733): archivo ZIP con los
    /// CSVs de todos sus datos personales y clínicos. Distinto de <see cref="ExportPdf"/>, que es el
    /// reporte clínico en PDF del nutricionista.
    /// </summary>
    Export,

    /// <summary>
    /// Registro de fallback del orquestador del modelo de lenguaje: la explicación de una recomendación
    /// se generó con el proveedor de respaldo tras un fallo o timeout de Ollama (TS08 CA02).
    /// </summary>
    LlmFallback,

    /// <summary>
    /// Renovación exitosa de la sesión mediante refresh token.
    /// </summary>
    TokenRefresh,

    /// <summary>
    /// Intento fallido de renovación: el refresh token estaba vencido, revocado o ya consumido.
    /// </summary>
    FailedTokenRefresh,

    /// <summary>
    /// Solicitud de reenvío del correo de verificación (acta A40). Se registra siempre que la petición
    /// supere la validación, exista o no la cuenta y esté o no verificada, porque la respuesta es
    /// deliberadamente uniforme y la bitácora es el único lugar donde queda el intento.
    /// </summary>
    VerificationEmailResendRequest,

    /// <summary>
    /// Canje de un código de invitación para vincular a un paciente con su nutricionista después del
    /// registro (acta A41). Se registra tanto el canje efectivo como el rechazado, con el motivo en el
    /// contexto adicional, porque un rechazo es justamente lo que interesa investigar después.
    /// </summary>
    NutritionistAssignment,

    /// <summary>
    /// Activación de una cuenta de nutricionista pendiente al autenticarse por primera vez (acta A51).
    /// La tabla <c>users</c> no tiene trigger de auditoría, así que esta fila es el único rastro de la
    /// transición de estado. El contexto adicional indica cuál de los dos puntos de entrada la disparó.
    /// </summary>
    AccountActivation,

    /// <summary>
    /// Reenvío, por el endpoint administrativo, del enlace con el que un nutricionista pendiente define su
    /// contraseña (acta A52). Se registra solo cuando Keycloak aceptó el envío.
    /// </summary>
    ActivationEmailResend
}
