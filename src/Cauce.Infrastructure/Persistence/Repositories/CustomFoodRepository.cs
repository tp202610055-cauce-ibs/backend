using Cauce.Application.Common.Interfaces.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementación de <see cref="ICustomFoodRepository"/> sobre <see cref="CauceDbContext"/>.
/// </summary>
public sealed class CustomFoodRepository : ICustomFoodRepository
{
    private readonly CauceDbContext _context;

    /// <summary>
    /// Inicializa el repositorio con el contexto de base de datos.
    /// </summary>
    /// <param name="context">Contexto de Entity Framework Core.</param>
    public CustomFoodRepository(CauceDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public Task<CustomFood?> FindByIdAsync(Guid customFoodId, CancellationToken ct = default)
    {
        return _context.Set<CustomFood>().FirstOrDefaultAsync(x => x.Id == customFoodId, ct);
    }

    /// <inheritdoc />
    public Task<CustomFood?> FindByIdWithIngredientsAsync(Guid customFoodId, CancellationToken ct = default)
    {
        return _context.Set<CustomFood>()
            .Include(x => x.Ingredients)
            .FirstOrDefaultAsync(x => x.Id == customFoodId, ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CustomFood>> ListByPatientAsync(Guid patientId, CancellationToken ct = default)
    {
        return await _context.Set<CustomFood>()
            .AsNoTracking()
            .Include(x => x.Ingredients)
            .Where(x => x.PatientId == patientId)
            .OrderBy(x => x.Name)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<bool> ExistsByPatientAndNameAsync(Guid patientId, string name, CancellationToken ct = default)
    {
        return _context.Set<CustomFood>()
            .AsNoTracking()
            .AnyAsync(x => x.PatientId == patientId && x.Name == name, ct);
    }

    /// <inheritdoc />
    public Task<bool> IsReferencedByMealItemsAsync(Guid customFoodId, CancellationToken ct = default)
    {
        return _context.Set<MealItem>()
            .AsNoTracking()
            .AnyAsync(x => x.CustomFoodId == customFoodId, ct);
    }

    /// <inheritdoc />
    public async Task AddAsync(CustomFood customFood, CancellationToken ct = default)
    {
        await _context.Set<CustomFood>().AddAsync(customFood, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Remove(CustomFood customFood)
    {
        _context.Set<CustomFood>().Remove(customFood);
    }
}
