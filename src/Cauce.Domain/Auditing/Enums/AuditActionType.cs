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
    PasswordResetConfirm
}
