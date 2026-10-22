using Cauce.Application.Common.Interfaces.Storage;
using Cauce.Infrastructure.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cauce.Infrastructure.Persistence.Seeders;

/// <summary>
/// Seeder idempotente que asegura los buckets de reportes y de exportaciones en MinIO (DEC-B5-07).
/// <see cref="SeedAsync"/> tolera un MinIO caído; <see cref="EnsureBucketsAsync"/> no.
/// </summary>
public sealed class MinioBucketSeeder
{
    private readonly IObjectStorage _objectStorage;
    private readonly MinioOptions _options;
    private readonly ILogger<MinioBucketSeeder> _logger;

    /// <summary>
    /// Inicializa el seeder con sus dependencias.
    /// </summary>
    /// <param name="objectStorage">Almacenamiento de objetos.</param>
    /// <param name="options">Opciones de MinIO.</param>
    /// <param name="logger">Logger de la categoría del seeder.</param>
    public MinioBucketSeeder(
        IObjectStorage objectStorage,
        IOptions<MinioOptions> options,
        ILogger<MinioBucketSeeder> logger)
    {
        _objectStorage = objectStorage;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Asegura la existencia del bucket de reportes de forma idempotente.
    /// </summary>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Tarea que representa la operación asíncrona.</returns>
    public async Task SeedAsync(CancellationToken ct = default)
    {
        foreach (var bucket in Buckets)
        {
            try
            {
                await EnsureBucketAsync(bucket, ct).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Could not ensure MinIO bucket {Bucket}; object storage may be unavailable.", bucket);
            }
        }
    }

    /// <summary>
    /// Asegura la existencia de los buckets y propaga el error si MinIO no responde.
    /// </summary>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Tarea que representa la operación asíncrona.</returns>
    public async Task EnsureBucketsAsync(CancellationToken ct = default)
    {
        foreach (var bucket in Buckets)
        {
            await EnsureBucketAsync(bucket, ct).ConfigureAwait(false);
        }
    }

    private IEnumerable<string> Buckets => [_options.ReportsBucket, _options.ExportsBucket];

    private async Task EnsureBucketAsync(string bucket, CancellationToken ct)
    {
        await _objectStorage.EnsureBucketExistsAsync(bucket, ct).ConfigureAwait(false);
        _logger.LogInformation("Ensured MinIO bucket {Bucket} exists.", bucket);
    }
}
