using Cauce.Application.Common.Interfaces.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementación de <see cref="IGlossaryRepository"/> sobre <see cref="CauceDbContext"/>. La búsqueda
/// usa <c>unaccent</c> + <c>ILIKE</c> para ser insensible a mayúsculas y a tildes (US27). Si la
/// extensión <c>unaccent</c> no estuviera disponible en algún entorno, la búsqueda debería degradar a
/// <c>ILIKE</c> simple (acta A27).
/// </summary>
public sealed class GlossaryRepository : IGlossaryRepository
{
    private readonly CauceDbContext _context;

    /// <summary>
    /// Inicializa el repositorio con el contexto de base de datos.
    /// </summary>
    /// <param name="context">Contexto de Entity Framework Core.</param>
    public GlossaryRepository(CauceDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GlossaryTerm>> ListAllOrderedAsync(CancellationToken ct = default)
    {
        return await _context.GlossaryTerms
            .AsNoTracking()
            .OrderBy(term => term.Term)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GlossaryTerm>> SearchAsync(string query, CancellationToken ct = default)
    {
        var pattern = $"%{query}%";

        return await _context.GlossaryTerms
            .AsNoTracking()
            .Where(term =>
                EF.Functions.ILike(EF.Functions.Unaccent(term.Term), EF.Functions.Unaccent(pattern))
                || EF.Functions.ILike(EF.Functions.Unaccent(term.PatientDefinition), EF.Functions.Unaccent(pattern))
                || EF.Functions.ILike(EF.Functions.Unaccent(term.NutritionistDefinition), EF.Functions.Unaccent(pattern)))
            .OrderBy(term => term.Term)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }
}
