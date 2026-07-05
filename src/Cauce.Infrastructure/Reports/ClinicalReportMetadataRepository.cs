using Cauce.Application.Common.Interfaces.Reports;
using Cauce.Domain.Reports;
using Cauce.Infrastructure.Persistence;

namespace Cauce.Infrastructure.Reports;

/// <summary>
/// Implementación de <see cref="IClinicalReportMetadataRepository"/> sobre <see cref="CauceDbContext"/>.
/// </summary>
public sealed class ClinicalReportMetadataRepository : IClinicalReportMetadataRepository
{
    private readonly CauceDbContext _context;

    /// <summary>
    /// Inicializa el repositorio con el contexto de base de datos.
    /// </summary>
    /// <param name="context">Contexto de Entity Framework Core.</param>
    public ClinicalReportMetadataRepository(CauceDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task AddAsync(ClinicalReportMetadata metadata, CancellationToken ct = default)
    {
        await _context.ClinicalReportsMetadata.AddAsync(metadata, ct).ConfigureAwait(false);
    }
}
