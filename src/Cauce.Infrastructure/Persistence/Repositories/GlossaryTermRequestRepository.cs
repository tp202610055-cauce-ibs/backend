using Cauce.Application.Common.Interfaces.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry.Enums;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementación de <see cref="IGlossaryTermRequestRepository"/> sobre <see cref="CauceDbContext"/>.
/// </summary>
public sealed class GlossaryTermRequestRepository : IGlossaryTermRequestRepository
{
    private readonly CauceDbContext _context;

    /// <summary>
    /// Inicializa el repositorio con el contexto de base de datos.
    /// </summary>
    public GlossaryTermRequestRepository(CauceDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public Task<GlossaryTermRequest?> FindPendingAsync(Guid nutritionistId, string term, CancellationToken ct = default)
    {
        return _context.GlossaryTermRequests
            .AsNoTracking()
            .Where(request => request.NutritionistId == nutritionistId
                && request.Status == GlossaryTermRequestStatus.Pending
                && EF.Functions.Unaccent(request.Term).ToLower() == EF.Functions.Unaccent(term).ToLower())
            .OrderBy(request => request.RequestedAt)
            .FirstOrDefaultAsync(ct);
    }

    /// <inheritdoc />
    public async Task AddAsync(GlossaryTermRequest request, CancellationToken ct = default)
    {
        await _context.GlossaryTermRequests.AddAsync(request, ct).ConfigureAwait(false);
    }
}
