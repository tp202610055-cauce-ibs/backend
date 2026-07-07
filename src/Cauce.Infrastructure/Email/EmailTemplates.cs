namespace Cauce.Infrastructure.Email;

/// <summary>
/// Plantillas de correo en español, en versiones de texto plano y HTML. No usan
/// recursos externos para que rendericen correctamente en cualquier cliente.
/// </summary>
public static class EmailTemplates
{
    /// <summary>
    /// Asunto del correo de credenciales temporales del nutricionista.
    /// </summary>
    public const string NutritionistCredentialsSubject = "Cauce — Credenciales de acceso";

    /// <summary>
    /// Asunto del correo de restablecimiento de contraseña.
    /// </summary>
    public const string PasswordResetSubject = "Cauce — Restablecimiento de contraseña";

    /// <summary>
    /// Asunto del correo que notifica que el reporte clínico está disponible.
    /// </summary>
    public const string ReportReadySubject = "Cauce — Reporte clínico disponible";

    /// <summary>
    /// Asunto del correo que envía la contraseña del reporte clínico.
    /// </summary>
    public const string ReportPasswordSubject = "Cauce — Contraseña de su reporte clínico";

    /// <summary>
    /// Asunto del correo que confirma la eliminación (anonimización) de la cuenta del paciente.
    /// </summary>
    public const string AccountDeletionSubject = "Cauce — Confirmación de eliminación de su cuenta";

    /// <summary>
    /// Construye el cuerpo en texto plano del correo de credenciales del nutricionista.
    /// </summary>
    /// <param name="fullName">Nombre completo del destinatario.</param>
    /// <param name="email">Correo de acceso.</param>
    /// <param name="temporaryPassword">Contraseña temporal asignada.</param>
    /// <param name="appBaseUrl">URL base del portal.</param>
    /// <returns>Cuerpo en texto plano.</returns>
    public static string BuildNutritionistCredentialsText(
        string fullName,
        string email,
        string temporaryPassword,
        string appBaseUrl)
    {
        return $"""
            Hola {fullName},

            Se ha creado una cuenta en el sistema Cauce para que pueda revisar las recomendaciones dietéticas del piloto clínico.

            Sus credenciales de acceso son:
              Correo: {email}
              Contraseña temporal: {temporaryPassword}

            Por favor ingrese al portal en {appBaseUrl} y cambie su contraseña en el primer acceso. La contraseña temporal vence cuando complete su primer inicio de sesión.

            Si no esperaba este correo, por favor ignórelo o contacte al equipo de soporte.

            Equipo Cauce
            """;
    }

    /// <summary>
    /// Construye el cuerpo HTML del correo de credenciales del nutricionista.
    /// </summary>
    /// <param name="fullName">Nombre completo del destinatario.</param>
    /// <param name="email">Correo de acceso.</param>
    /// <param name="temporaryPassword">Contraseña temporal asignada.</param>
    /// <param name="appBaseUrl">URL base del portal.</param>
    /// <returns>Cuerpo HTML.</returns>
    public static string BuildNutritionistCredentialsHtml(
        string fullName,
        string email,
        string temporaryPassword,
        string appBaseUrl)
    {
        return $"""
            <p>Hola {fullName},</p>
            <p>Se ha creado una cuenta en el sistema Cauce para que pueda revisar las recomendaciones dietéticas del piloto clínico.</p>
            <p>Sus credenciales de acceso son:</p>
            <ul>
              <li><strong>Correo:</strong> {email}</li>
              <li><strong>Contraseña temporal:</strong> {temporaryPassword}</li>
            </ul>
            <p>Por favor ingrese al portal en <a href="{appBaseUrl}">{appBaseUrl}</a> y cambie su contraseña en el primer acceso. La contraseña temporal vence cuando complete su primer inicio de sesión.</p>
            <p>Si no esperaba este correo, por favor ignórelo o contacte al equipo de soporte.</p>
            <p>Equipo Cauce</p>
            """;
    }

    /// <summary>
    /// Construye el cuerpo en texto plano del correo de restablecimiento de contraseña.
    /// </summary>
    /// <param name="fullName">Nombre completo del destinatario.</param>
    /// <param name="resetLink">Enlace de restablecimiento.</param>
    /// <returns>Cuerpo en texto plano.</returns>
    public static string BuildPasswordResetText(string fullName, string resetLink)
    {
        return $"""
            Hola {fullName},

            Hemos recibido una solicitud para restablecer la contraseña de su cuenta Cauce.

            Para crear una nueva contraseña, ingrese al siguiente enlace dentro de los próximos 30 minutos:

            {resetLink}

            Si usted no solicitó este restablecimiento, puede ignorar este correo. Su contraseña actual seguirá siendo válida.

            Equipo Cauce
            """;
    }

    /// <summary>
    /// Construye el cuerpo HTML del correo de restablecimiento de contraseña.
    /// </summary>
    /// <param name="fullName">Nombre completo del destinatario.</param>
    /// <param name="resetLink">Enlace de restablecimiento.</param>
    /// <returns>Cuerpo HTML.</returns>
    public static string BuildPasswordResetHtml(string fullName, string resetLink)
    {
        return $"""
            <p>Hola {fullName},</p>
            <p>Hemos recibido una solicitud para restablecer la contraseña de su cuenta Cauce.</p>
            <p>Para crear una nueva contraseña, ingrese al siguiente enlace dentro de los próximos 30 minutos:</p>
            <p><a href="{resetLink}">{resetLink}</a></p>
            <p>Si usted no solicitó este restablecimiento, puede ignorar este correo. Su contraseña actual seguirá siendo válida.</p>
            <p>Equipo Cauce</p>
            """;
    }

    /// <summary>
    /// Construye el cuerpo en texto plano del correo que notifica el reporte disponible.
    /// </summary>
    /// <param name="fullName">Nombre del nutricionista.</param>
    /// <param name="presignedUrl">URL prefirmada de descarga.</param>
    /// <param name="expiresAtUtc">Momento de expiración de la URL, en UTC.</param>
    /// <returns>Cuerpo en texto plano.</returns>
    public static string BuildReportReadyText(string fullName, string presignedUrl, DateTime expiresAtUtc)
    {
        return $"""
            Hola {fullName},

            El reporte clínico que solicitó ya está disponible. Puede descargarlo desde el siguiente enlace, válido hasta el {expiresAtUtc:yyyy-MM-dd HH:mm} UTC:

            {presignedUrl}

            El archivo está protegido con una contraseña que le enviaremos en un correo separado, por su seguridad.

            Equipo Cauce
            """;
    }

    /// <summary>
    /// Construye el cuerpo HTML del correo que notifica el reporte disponible.
    /// </summary>
    /// <param name="fullName">Nombre del nutricionista.</param>
    /// <param name="presignedUrl">URL prefirmada de descarga.</param>
    /// <param name="expiresAtUtc">Momento de expiración de la URL, en UTC.</param>
    /// <returns>Cuerpo HTML.</returns>
    public static string BuildReportReadyHtml(string fullName, string presignedUrl, DateTime expiresAtUtc)
    {
        return $"""
            <p>Hola {fullName},</p>
            <p>El reporte clínico que solicitó ya está disponible. Puede descargarlo desde el siguiente enlace, válido hasta el {expiresAtUtc:yyyy-MM-dd HH:mm} UTC:</p>
            <p><a href="{presignedUrl}">Descargar reporte</a></p>
            <p>El archivo está protegido con una contraseña que le enviaremos en un correo separado, por su seguridad.</p>
            <p>Equipo Cauce</p>
            """;
    }

    /// <summary>
    /// Construye el cuerpo en texto plano del correo con la contraseña del reporte.
    /// </summary>
    /// <param name="fullName">Nombre del nutricionista.</param>
    /// <param name="password">Contraseña del PDF cifrado.</param>
    /// <returns>Cuerpo en texto plano.</returns>
    public static string BuildReportPasswordText(string fullName, string password)
    {
        return $"""
            Hola {fullName},

            La contraseña para abrir el reporte clínico que le enviamos es:

            {password}

            No comparta esta contraseña. El reporte contiene datos clínicos sensibles protegidos por la Ley N.° 29733.

            Equipo Cauce
            """;
    }

    /// <summary>
    /// Construye el cuerpo HTML del correo con la contraseña del reporte.
    /// </summary>
    /// <param name="fullName">Nombre del nutricionista.</param>
    /// <param name="password">Contraseña del PDF cifrado.</param>
    /// <returns>Cuerpo HTML.</returns>
    public static string BuildReportPasswordHtml(string fullName, string password)
    {
        return $"""
            <p>Hola {fullName},</p>
            <p>La contraseña para abrir el reporte clínico que le enviamos es:</p>
            <p style="font-size:1.2em;"><strong>{password}</strong></p>
            <p>No comparta esta contraseña. El reporte contiene datos clínicos sensibles protegidos por la Ley N.° 29733.</p>
            <p>Equipo Cauce</p>
            """;
    }

    /// <summary>
    /// Construye el cuerpo en texto plano del correo de confirmación de eliminación de cuenta.
    /// </summary>
    /// <param name="fullName">Nombre completo original del paciente.</param>
    /// <returns>Cuerpo en texto plano.</returns>
    public static string BuildAccountDeletionText(string fullName)
    {
        return $"""
            Hola {fullName},

            Confirmamos que su cuenta en Cauce fue eliminada a su solicitud. Sus datos personales fueron anonimizados y ya no podrá iniciar sesión.

            Por requerimientos de trazabilidad clínica y de la Ley N.° 29733, algunos registros se conservan de forma anonimizada, sin posibilidad de vincularlos nuevamente con su identidad.

            Si usted no solicitó esta eliminación, contacte de inmediato al equipo de soporte.

            Equipo Cauce
            """;
    }

    /// <summary>
    /// Construye el cuerpo HTML del correo de confirmación de eliminación de cuenta.
    /// </summary>
    /// <param name="fullName">Nombre completo original del paciente.</param>
    /// <returns>Cuerpo HTML.</returns>
    public static string BuildAccountDeletionHtml(string fullName)
    {
        return $"""
            <p>Hola {fullName},</p>
            <p>Confirmamos que su cuenta en Cauce fue eliminada a su solicitud. Sus datos personales fueron anonimizados y ya no podrá iniciar sesión.</p>
            <p>Por requerimientos de trazabilidad clínica y de la Ley N.° 29733, algunos registros se conservan de forma anonimizada, sin posibilidad de vincularlos nuevamente con su identidad.</p>
            <p>Si usted no solicitó esta eliminación, contacte de inmediato al equipo de soporte.</p>
            <p>Equipo Cauce</p>
            """;
    }
}
