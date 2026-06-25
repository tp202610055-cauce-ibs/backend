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
}
