using System.Diagnostics.CodeAnalysis;

namespace Cauce.Application.Common.Interfaces.Identity;

/// <summary>
/// Caché en memoria del catálogo de roles (<c>user_roles</c>), que traduce el identificador numérico
/// del rol a su nombre canónico. El catálogo es fijo y no cambia en tiempo de ejecución, así que se
/// carga una sola vez por proceso.
/// </summary>
/// <remarks>
/// Se registra como singleton. Un caché por instancia de repositorio no serviría de nada: el
/// repositorio es de ámbito por petición y se reconstruye en cada una.
/// </remarks>
public interface IRoleNameCache
{
    /// <summary>
    /// Intenta obtener el nombre de un rol ya cacheado.
    /// </summary>
    /// <param name="roleId">Identificador del rol en el catálogo.</param>
    /// <param name="roleName">Nombre del rol, si está cacheado.</param>
    /// <returns><see langword="true"/> si el rol estaba en el caché.</returns>
    bool TryGetName(int roleId, [MaybeNullWhen(false)] out string roleName);

    /// <summary>
    /// Almacena el catálogo completo de roles. Es idempotente y seguro entre hilos: varias peticiones
    /// concurrentes pueden cargarlo a la vez sin corromper el estado.
    /// </summary>
    /// <param name="catalog">Catálogo de roles indexado por identificador.</param>
    void Store(IReadOnlyDictionary<int, string> catalog);
}
