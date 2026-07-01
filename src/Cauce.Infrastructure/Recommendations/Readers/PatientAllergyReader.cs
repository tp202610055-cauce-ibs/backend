using Cauce.Application.Common.Interfaces.Recommendations;
using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.Patients;
using Cauce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Infrastructure.Recommendations.Readers;

/// <summary>
/// Implementación de <see cref="IPatientAllergyReader"/> para el guardrail duro pre-inferencia
/// (DEC-B4-09). Obtiene los nombres de las alergias declaradas por el paciente y delega la
/// detección de alimentos prohibidos al <see cref="AllergyHeuristicMatcher"/> (DEC-B4-14). El
/// criterio de detección es deliberadamente conservador: ante la duda, se excluye el alimento.
/// </summary>
public sealed class PatientAllergyReader : IPatientAllergyReader
{
    private readonly CauceDbContext _context;
    private readonly AllergyHeuristicMatcher _matcher;

    /// <summary>
    /// Inicializa el lector con sus dependencias.
    /// </summary>
    /// <param name="context">Contexto de Entity Framework Core.</param>
    /// <param name="matcher">Matcher heurístico de alergias.</param>
    public PatientAllergyReader(CauceDbContext context, AllergyHeuristicMatcher matcher)
    {
        _context = context;
        _matcher = matcher;
    }

    /// <inheritdoc />
    public async Task<HashSet<Guid>> GetAllergyFoodIdsAsync(Guid patientId, CancellationToken ct = default)
    {
        var allergyNames = await _context.Set<PatientAllergy>()
            .Where(declaration => declaration.PatientId == patientId)
            .Join(
                _context.Set<Allergy>(),
                declaration => declaration.AllergyId,
                allergy => allergy.Id,
                (declaration, allergy) => allergy.Name)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (allergyNames.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var foods = await _context.Set<FoodItem>()
            .AsNoTracking()
            .Where(food => food.IsActive)
            .Select(food => new FoodCatalogEntry(food.Id, food.Name, food.Category, food.FodmapTags))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return _matcher.GetForbiddenFoodIds(allergyNames, foods);
    }
}
