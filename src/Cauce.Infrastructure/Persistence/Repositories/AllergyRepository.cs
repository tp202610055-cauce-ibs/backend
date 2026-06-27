using Cauce.Application.Common.Interfaces.Patients;
using Cauce.Domain.Patients;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementación de <see cref="IAllergyRepository"/> sobre <see cref="CauceDbContext"/>.
/// </summary>
public sealed class AllergyRepository : IAllergyRepository
{
    private readonly CauceDbContext _context;

    /// <summary>
    /// Inicializa el repositorio con el contexto de base de datos.
    /// </summary>
    /// <param name="context">Contexto de Entity Framework Core.</param>
    public AllergyRepository(CauceDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Allergy>> ListActiveAsync(CancellationToken ct = default)
    {
        return await _context.Set<Allergy>()
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<Allergy?> FindByIdAsync(Guid allergyId, CancellationToken ct = default)
    {
        return _context.Set<Allergy>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == allergyId, ct);
    }

    /// <inheritdoc />
    public async Task AddAsync(Allergy allergy, CancellationToken ct = default)
    {
        await _context.Set<Allergy>().AddAsync(allergy, ct).ConfigureAwait(false);
    }
}
