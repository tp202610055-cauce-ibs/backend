using Cauce.Application.Common.Interfaces.Patients;
using Cauce.Domain.Patients;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementación de <see cref="IPatientAllergyRepository"/> sobre <see cref="CauceDbContext"/>.
/// </summary>
public sealed class PatientAllergyRepository : IPatientAllergyRepository
{
    private readonly CauceDbContext _context;

    /// <summary>
    /// Inicializa el repositorio con el contexto de base de datos.
    /// </summary>
    /// <param name="context">Contexto de Entity Framework Core.</param>
    public PatientAllergyRepository(CauceDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PatientAllergy>> ListByPatientAsync(Guid patientId, CancellationToken ct = default)
    {
        return await _context.Set<PatientAllergy>()
            .AsNoTracking()
            .Where(x => x.PatientId == patientId)
            .OrderByDescending(x => x.DeclaredAt)
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<PatientAllergy?> FindByIdAsync(Guid patientAllergyId, CancellationToken ct = default)
    {
        return _context.Set<PatientAllergy>().FirstOrDefaultAsync(x => x.Id == patientAllergyId, ct);
    }

    /// <inheritdoc />
    public Task<bool> ExistsAsync(Guid patientId, Guid allergyId, CancellationToken ct = default)
    {
        return _context.Set<PatientAllergy>()
            .AsNoTracking()
            .AnyAsync(x => x.PatientId == patientId && x.AllergyId == allergyId, ct);
    }

    /// <inheritdoc />
    public async Task AddAsync(PatientAllergy patientAllergy, CancellationToken ct = default)
    {
        await _context.Set<PatientAllergy>().AddAsync(patientAllergy, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Remove(PatientAllergy patientAllergy)
    {
        _context.Set<PatientAllergy>().Remove(patientAllergy);
    }
}
