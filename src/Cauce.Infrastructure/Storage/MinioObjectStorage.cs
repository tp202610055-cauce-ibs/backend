using Cauce.Application.Common.Interfaces.Storage;
using Microsoft.Extensions.Logging;
using Minio;
using Minio.DataModel.Args;

namespace Cauce.Infrastructure.Storage;

/// <summary>
/// Implementación de <see cref="IObjectStorage"/> sobre MinIO (S3-compatible). Sube objetos, genera
/// URLs prefirmadas de descarga y asegura la existencia de buckets de forma idempotente.
/// </summary>
public sealed class MinioObjectStorage : IObjectStorage
{
    private readonly IMinioClient _minioClient;
    private readonly ILogger<MinioObjectStorage> _logger;

    /// <summary>
    /// Inicializa el almacenamiento con el cliente de MinIO.
    /// </summary>
    /// <param name="minioClient">Cliente de MinIO.</param>
    /// <param name="logger">Logger de la categoría del almacenamiento.</param>
    public MinioObjectStorage(IMinioClient minioClient, ILogger<MinioObjectStorage> logger)
    {
        _minioClient = minioClient;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string> UploadAsync(
        string bucket,
        string objectKey,
        Stream content,
        string contentType,
        CancellationToken ct = default)
    {
        await EnsureBucketExistsAsync(bucket, ct).ConfigureAwait(false);

        var putArgs = new PutObjectArgs()
            .WithBucket(bucket)
            .WithObject(objectKey)
            .WithStreamData(content)
            .WithObjectSize(content.Length)
            .WithContentType(contentType);

        await _minioClient.PutObjectAsync(putArgs, ct).ConfigureAwait(false);
        _logger.LogInformation("Object {ObjectKey} uploaded to bucket {Bucket}.", objectKey, bucket);
        return objectKey;
    }

    /// <inheritdoc />
    public async Task<string> GetPresignedUrlAsync(
        string bucket,
        string objectKey,
        TimeSpan validity,
        CancellationToken ct = default)
    {
        var presignedArgs = new PresignedGetObjectArgs()
            .WithBucket(bucket)
            .WithObject(objectKey)
            .WithExpiry((int)validity.TotalSeconds);

        return await _minioClient.PresignedGetObjectAsync(presignedArgs).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task EnsureBucketExistsAsync(string bucket, CancellationToken ct = default)
    {
        var exists = await _minioClient
            .BucketExistsAsync(new BucketExistsArgs().WithBucket(bucket), ct)
            .ConfigureAwait(false);

        if (!exists)
        {
            await _minioClient
                .MakeBucketAsync(new MakeBucketArgs().WithBucket(bucket), ct)
                .ConfigureAwait(false);
            _logger.LogInformation("Bucket {Bucket} created.", bucket);
        }
    }
}
