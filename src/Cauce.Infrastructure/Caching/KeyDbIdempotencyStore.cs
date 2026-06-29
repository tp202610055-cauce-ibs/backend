using System.Text.Json;
using Cauce.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Cauce.Infrastructure.Caching;

/// <summary>
/// Implementación de <see cref="IIdempotencyStore"/> respaldada por KeyDB (compatible con
/// Redis) mediante <c>StackExchange.Redis</c>. Si KeyDB no está disponible, las
/// operaciones degradan de forma silenciosa (fail-open): la idempotencia deja de
/// detectar reintentos, pero el sistema continúa y la restricción única de
/// <c>client_guid</c> en base de datos actúa como red de seguridad final.
/// </summary>
public sealed class KeyDbIdempotencyStore : IIdempotencyStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<KeyDbIdempotencyStore> _logger;

    /// <summary>
    /// Inicializa el almacén con el multiplexor de conexión y el logger.
    /// </summary>
    /// <param name="redis">Multiplexor de conexión a KeyDB.</param>
    /// <param name="logger">Logger de la categoría del almacén.</param>
    public KeyDbIdempotencyStore(IConnectionMultiplexer redis, ILogger<KeyDbIdempotencyStore> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<TResult?> GetAsync<TResult>(string key, CancellationToken ct = default)
    {
        try
        {
            var database = _redis.GetDatabase();
            var value = await database.StringGetAsync(key).ConfigureAwait(false);
            if (value.IsNullOrEmpty)
            {
                return default;
            }

            return JsonSerializer.Deserialize<TResult>(value!, JsonOptions);
        }
        catch (RedisConnectionException exception)
        {
            _logger.LogWarning(exception, "KeyDB unavailable; bypassing idempotency for key {Key}", key);
            return default;
        }
    }

    /// <inheritdoc />
    public async Task SetAsync<TResult>(string key, TResult value, TimeSpan ttl, CancellationToken ct = default)
    {
        try
        {
            var database = _redis.GetDatabase();
            var serialized = JsonSerializer.Serialize(value, JsonOptions);
            await database.StringSetAsync(key, serialized, ttl).ConfigureAwait(false);
        }
        catch (RedisConnectionException exception)
        {
            _logger.LogWarning(exception, "KeyDB unavailable; idempotency entry not persisted for key {Key}", key);
        }
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        try
        {
            return await _redis.GetDatabase().KeyExistsAsync(key).ConfigureAwait(false);
        }
        catch (RedisConnectionException)
        {
            return false;
        }
    }
}
