using Cauce.Infrastructure.Storage;
using FluentAssertions;
using Minio.DataModel.Args;

namespace Cauce.Infrastructure.Tests.Storage;

/// <summary>
/// Pruebas de la firma de URLs de MinIO: firmar no consulta la red, así que funciona aunque el host público no
/// resuelva desde donde corre el backend (en la VM, sin pasar por la IP pública).
/// </summary>
public sealed class MinioClientSigningTests
{
    [Fact]
    public async Task PresignedGetObject_AgainstAnUnresolvableHost_SignsWithoutNetwork()
    {
        var options = new MinioOptions
        {
            Endpoint = "minio:9000",
            AccessKey = "test-access-key",
            SecretKey = "test-secret-key",
            PublicEndpoint = "archivos.example.test",
            PublicUseSsl = true
        };
        var client = DependencyInjection.BuildMinioClient(options.PublicEndpoint, options.PublicUseSsl, options);

        var url = await client.PresignedGetObjectAsync(new PresignedGetObjectArgs()
            .WithBucket("clinical-reports")
            .WithObject("reporte.pdf")
            .WithExpiry(3600));

        url.Should().StartWith("https://archivos.example.test/clinical-reports/reporte.pdf?");
        url.Should().Contain("us-east-1");
    }
}
