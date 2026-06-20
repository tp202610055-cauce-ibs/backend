using Microsoft.AspNetCore.Identity;

namespace Cauce.Api.Domain.Entities;

/// <summary>
/// Entidad de usuario del sistema. Representa a los dos actores funcionales
/// (paciente con SII y dietista-nutricionista), diferenciados por RoleId.
///
/// Extiende IdentityUser&lt;Guid&gt; temporalmente por adenda a DEC-009: la
/// gestión de credenciales delegada a Keycloak en el diseño original se
/// reemplaza por ASP.NET Core Identity en el MVP. Las propiedades heredadas
/// de IdentityUser relacionadas con autenticación (PasswordHash, SecurityStamp,
/// ConcurrencyStamp, etc.) son temporales y se eliminan al reincorporar
/// Keycloak en v0.2.0.
/// </summary>
public class User : IdentityUser<Guid>
{
    /// <summary>Nombre completo del usuario tal como fue ingresado durante el registro.</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>FK al rol asignado (1 = patient, 2 = nutritionist).</summary>
    public int RoleId { get; set; }

    /// <summary>Estado operativo: pending_activation, active, inactive, suspended.</summary>
    public string Status { get; set; } = "pending_activation";

    /// <summary>
    /// Identificador en Keycloak. Nullable durante el MVP (sin Keycloak).
    /// Se poblará por script de migración al reincorporar Keycloak en v0.2.0.
    /// </summary>
    public string? KeycloakId { get; set; }

    /// <summary>Momento de creación de la cuenta. UTC.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Momento de la última modificación del registro. UTC.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Momento del último inicio de sesión exitoso. UTC.</summary>
    public DateTime? LastLoginAt { get; set; }

    // Navigation property
    public UserRole? Role { get; set; }
}