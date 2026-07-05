namespace Cauce.Application.Common.Auditing;

/// <summary>
/// Utilidades para ofuscar identificadores personales antes de escribirlos en el
/// contexto adicional de auditoría. La bitácora no debe contener PII cruda (Ley N.° 29733);
/// estas funciones producen representaciones parciales suficientes para la trazabilidad.
/// </summary>
public static class AuditMask
{
    /// <summary>
    /// Enmascara un correo electrónico conservando la primera letra de la parte local y el
    /// dominio completo (por ejemplo, <c>juan@dominio.com</c> se convierte en <c>j***@dominio.com</c>).
    /// </summary>
    /// <param name="email">Correo a enmascarar.</param>
    /// <returns>Correo enmascarado, o una marca genérica si el formato no es válido.</returns>
    public static string Email(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return "***";
        }

        var atIndex = email.IndexOf('@');
        if (atIndex <= 0)
        {
            return "***";
        }

        var firstChar = email[0];
        var domain = email[atIndex..];
        return $"{firstChar}***{domain}";
    }
}
