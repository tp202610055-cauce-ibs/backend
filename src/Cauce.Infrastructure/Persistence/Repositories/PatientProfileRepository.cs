using Cauce.Application.Common.Interfaces.Patients;
using Cauce.Domain.Patients;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementación de <see cref="IPatientProfileRepository"/> sobre <see cref="CauceDbContext"/>.
/// </summary>
public sealed class PatientProfileRepository : IPatientProfileRepository
{
    private readonly CauceDbContext _context;

    /// <summary>
    /// Inicializa el repositorio con el contexto de base de datos.
    /// </summary>
    /// <param name="context">Contexto de Entity Framework Core.</param>
    public PatientProfileRepository(CauceDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public Task<PatientProfile?> FindByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        return _context.Set<PatientProfile>().FirstOrDefaultAsync(x => x.UserId == userId, ct);
    }

    /// <inheritdoc />
    public async Task AddAsync(PatientProfile profile, CancellationToken ct = default)
    {
        await _context.Set<PatientProfile>().AddAsync(profile, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<bool> ExistsByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        return _context.Set<PatientProfile>().AsNoTracking().AnyAsync(x => x.UserId == userId, ct);
    }
}
