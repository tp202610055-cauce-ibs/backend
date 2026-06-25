namespace Cauce.Infrastructure.Persistence.Entities;

/// <summary>
/// Entidad de persistencia que representa el catálogo de roles (<c>user_roles</c>).
/// No es una entidad de dominio: los roles se modelan como catálogo referenciado
/// por <c>users.role_id</c>. Su clave es autogenerada por la base de datos.
/// </summary>
public sealed class UserRoleEntity
{
    /// <summary>
    /// Identificador autogenerado del rol.
    /// </summary>
    public int RoleId { get; set; }

    /// <summary>
    /// Nombre único del rol.
    /// </summary>
    public string RoleName { get; set; } = string.Empty;

    /// <summary>
    /// Descripción del rol.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Indica si el rol está activo.
    /// </summary>
    public bool IsActive { get; set; } = true;
}
