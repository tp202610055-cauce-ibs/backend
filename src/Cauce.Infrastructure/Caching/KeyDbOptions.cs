namespace Cauce.Infrastructure.Caching;

/// <summary>
/// Opciones de conexión a KeyDB (compatible con Redis) usado como almacén de
/// idempotencia. Se enlaza con la sección <c>KeyDb</c> de la configuración.
/// </summary>
public sealed class KeyDbOptions
{
    /// <summary>
    /// Nombre de la sección de configuración.
    /// </summary>
    public const string SectionName = "KeyDb";

    /// <summary>
    /// Cadena de conexión a KeyDB.
    /// </summary>
    public string ConnectionString { get; init; } = "localhost:6379";
}
