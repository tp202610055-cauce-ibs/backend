namespace Cauce.Application.Common.Interfaces;

/// <summary>
/// Almacén de idempotencia respaldado por KeyDB. Guarda el resultado de comandos
/// idempotentes (junto con el hash de su carga) bajo una clave derivada del usuario y
/// del <c>client_guid</c>, para detectar reintentos y rechazar reusos con carga distinta.
/// </summary>
public interface IIdempotencyStore
{
    /// <summary>
    /// Obtiene el valor almacenado bajo la clave indicada.
    /// </summary>
    /// <typeparam name="TResult">Tipo del valor almacenado.</typeparam>
    /// <param name="key">Clave de idempotencia.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>El valor, o <see langword="null"/> si no existe o el almacén no está disponible.</returns>
    Task<TResult?> GetAsync<TResult>(string key, CancellationToken ct = default);

    /// <summary>
    /// Almacena el valor bajo la clave indicada con un tiempo de expiración.
    /// </summary>
    /// <typeparam name="TResult">Tipo del valor a almacenar.</typeparam>
    /// <param name="key">Clave de idempotencia.</param>
    /// <param name="value">Valor a almacenar.</param>
    /// <param name="ttl">Tiempo de vida de la entrada.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Tarea que representa la operación asíncrona.</returns>
    Task SetAsync<TResult>(string key, TResult value, TimeSpan ttl, CancellationToken ct = default);

    /// <summary>
    /// Indica si existe una entrada bajo la clave indicada.
    /// </summary>
    /// <param name="key">Clave de idempotencia.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns><see langword="true"/> si la clave existe.</returns>
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);
}
