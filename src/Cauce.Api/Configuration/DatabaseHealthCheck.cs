using Cauce.Infrastructure.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Cauce.Api.Configuration;

/// <summary>
/// Comprobación de salud que abre una conexión a la base de datos. La usa <c>/api/v1/health/ready</c>.
/// </summary>
internal sealed class DatabaseHealthCheck : IHealthCheck
{
    /// <summary>
    /// Etiqueta de las comprobaciones que solo cuenta la ruta <c>ready</c>.
    /// </summary>
    public const string ReadyTag = "ready";

    private readonly CauceDbContext _context;

    /// <summary>
    /// Inicializa la comprobación con el contexto de datos.
    /// </summary>
    public DatabaseHealthCheck(CauceDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var canConnect = await _context.Database.CanConnectAsync(cancellationToken).ConfigureAwait(false);
        return canConnect
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("La base de datos no responde.");
    }
}
