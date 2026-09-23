using Cauce.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

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

    /// <inheritdoc />
    public void DiscardTrackedChanges()
    {
        _context.ChangeTracker.Clear();
    }

    /// <inheritdoc />
    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);

        // Si ya hay una transacción abierta, abrir otra lanzaría. Se ejecuta dentro de la existente,
        // que es lo que el llamador quiere: atomicidad, no una transacción propia.
        if (_context.Database.CurrentTransaction is not null)
        {
            return await operation(cancellationToken).ConfigureAwait(false);
        }

        await using var transaction = await _context.Database
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        var result = await operation(cancellationToken).ConfigureAwait(false);

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }
}
