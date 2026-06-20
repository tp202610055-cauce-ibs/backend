namespace Cauce.Api.Domain.Entities;

/// <summary>
/// Catálogo cerrado de roles del sistema. Por diseño OE2, cada usuario tiene
/// exactamente un rol, lo que determina sus permisos y funcionalidades.
/// Valores permitidos para RoleName: "patient" y "nutritionist".
/// </summary>
public class UserRole
{
    /// <summary>Identificador interno del rol. Autoincremental.</summary>
    public int RoleId { get; set; }

    /// <summary>Nombre del rol. Valores: "patient" o "nutritionist".</summary>
    public string RoleName { get; set; } = string.Empty;

    /// <summary>Descripción funcional del rol para documentación interna.</summary>
    public string? Description { get; set; }

    /// <summary>Indica si el rol está habilitado para asignación a nuevos usuarios.</summary>
    public bool IsActive { get; set; } = true;

    // Navigation property
    public ICollection<User> Users { get; set; } = new List<User>();
}