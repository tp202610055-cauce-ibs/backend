using Cauce.Application.Common.Interfaces.Recommendations;
using Cauce.Domain.Recommendations;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementación de <see cref="IModelVersionRepository"/> sobre <see cref="CauceDbContext"/>.
/// </summary>
public sealed class ModelVersionRepository : IModelVersionRepository
{
    private readonly CauceDbContext _context;

    /// <summary>
    /// Inicializa el repositorio con el contexto de base de datos.
    /// </summary>
    /// <param name="context">Contexto de Entity Framework Core.</param>
    public ModelVersionRepository(CauceDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public Task<ModelVersion?> GetActiveAsync(CancellationToken ct = default)
    {
        return _context.Set<ModelVersion>().AsNoTracking().FirstOrDefaultAsync(x => x.IsActive, ct);
    }

    /// <inheritdoc />
    public Task<ModelVersion?> GetByIdAsync(Guid versionId, CancellationToken ct = default)
    {
        return _context.Set<ModelVersion>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == versionId, ct);
    }

    /// <inheritdoc />
    public Task<ModelVersion?> GetByNameAsync(string versionName, CancellationToken ct = default)
    {
        return _context.Set<ModelVersion>().AsNoTracking().FirstOrDefaultAsync(x => x.VersionName == versionName, ct);
    }

    /// <inheritdoc />
    public async Task AddAsync(ModelVersion modelVersion, CancellationToken ct = default)
    {
        await _context.Set<ModelVersion>().AddAsync(modelVersion, ct).ConfigureAwait(false);
    }
}
