using Cauce.Application.Common.Interfaces;

namespace Cauce.Infrastructure.Persistence;

/// <summary>
/// Implementación de <see cref="IUnitOfWork"/> que delega la confirmación de
/// cambios al <see cref="CauceDbContext"/>.
/// </summary>
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly CauceDbContext _context;

    /// <summary>
    /// Inicializa la unidad de trabajo con el contexto de base de datos.
    /// </summary>
    /// <param name="context">Contexto de Entity Framework Core.</param>
    public UnitOfWork(CauceDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
