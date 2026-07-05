using Cauce.Application.Common.Interfaces.Storage;
using Cauce.Infrastructure.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cauce.Infrastructure.Persistence.Seeders;

/// <summary>
/// Seeder idempotente que asegura la existencia del bucket de reportes clínicos en MinIO
/// (DEC-B5-07). No falla el arranque si MinIO no está disponible: registra la advertencia y continúa.
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
        try
        {
            await _objectStorage.EnsureBucketExistsAsync(_options.ReportsBucket, ct).ConfigureAwait(false);
            _logger.LogInformation("Ensured MinIO bucket {Bucket} exists.", _options.ReportsBucket);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Could not ensure MinIO bucket {Bucket}; object storage may be unavailable.", _options.ReportsBucket);
        }
    }
}
