namespace Cauce.Domain.Identity;

/// <summary>
/// Nombres canónicos de los roles del sistema. El rol se modela como tabla
/// catálogo <c>user_roles</c> con clave foránea desde <c>users.role_id</c>; estas
/// constantes representan los dos valores fijos del catálogo y deben usarse en
/// lugar de literales sueltos.
/// </summary>
public static class UserRoles
{
    /// <summary>
    /// Paciente con SII participante del piloto.
    /// </summary>
    public const string Patient = "patient";

    /// <summary>
    /// Dietista-nutricionista revisor clínico.
    /// </summary>
    public const string Nutritionist = "nutritionist";
}
