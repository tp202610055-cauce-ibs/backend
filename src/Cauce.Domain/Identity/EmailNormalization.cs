namespace Cauce.Domain.Identity;

/// <summary>
/// Criterio único de normalización del correo electrónico: sin espacios en los extremos y en minúsculas
/// invariantes (acta A70). Keycloak guarda y compara el correo en minúsculas; si la base local conserva la
/// capitalización con que se escribió, quien se registra como <c>Ana@…</c> y entra como <c>ana@…</c> es
/// autenticado por Keycloak y no encontrado localmente (<c>user_local_missing</c>).
///
/// <para>Lo usan la entidad <see cref="User"/> al guardar, el repositorio al buscar y cada flujo que manda
/// el correo a Keycloak: registro, inicio de sesión, reenvío de verificación, recuperación de contraseña,
/// provisión de nutricionistas y seeders. Las minúsculas son invariantes a propósito: con la cultura del
/// servidor, <c>ToLower()</c> convertiría <c>I</c> en <c>ı</c> bajo <c>tr-TR</c>.</para>
/// </summary>
public static class EmailNormalization
{
    /// <summary>
    /// Normaliza un correo electrónico. Un valor nulo se devuelve como cadena vacía para que la búsqueda
    /// simplemente no encuentre nada; la validación del formato es de los validadores de cada caso de uso.
    /// </summary>
    /// <param name="email">Correo tal como llegó.</param>
    /// <returns>El correo recortado y en minúsculas invariantes.</returns>
    public static string Normalize(string? email)
    {
        return email?.Trim().ToLowerInvariant() ?? string.Empty;
    }
}
