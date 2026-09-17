using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementación de <see cref="IConsentDocumentRepository"/> sobre EF Core.
/// </summary>
public sealed class ConsentDocumentRepository : IConsentDocumentRepository
{
    private readonly CauceDbContext _context;

    /// <summary>
    /// Inicializa el repositorio con el contexto de base de datos.
    /// </summary>
    /// <param name="context">Contexto de EF Core.</param>
    public ConsentDocumentRepository(CauceDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public Task<ConsentDocument?> FindByVersionAsync(string version, CancellationToken ct = default)
    {
        // AsNoTracking: estas filas son de solo lectura en todo el flujo de consulta.
        // Publicar una versión nueva es tarea del seeder, no de los handlers.
        return _context.ConsentDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Version == version, ct);
    }

    /// <inheritdoc />
    public Task<ConsentDocument?> FindCurrentAsync(CancellationToken ct = default)
    {
        return _context.ConsentDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.IsCurrent, ct);
    }
}
