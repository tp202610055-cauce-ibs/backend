using Cauce.Application.Common.Interfaces.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.Patients;
using Cauce.Infrastructure.Persistence;
using Cauce.Infrastructure.Recommendations.Readers;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Infrastructure.ClinicalRegistry.Readers;

/// <summary>
/// Implementación de <see cref="ICustomFoodAllergenChecker"/> (US10 CA03). Carga las alergias
/// declaradas del paciente y los ingredientes del catálogo, y ejecuta el
/// <see cref="AllergyHeuristicMatcher"/> <b>por cada alergia individual</b> para poder asociar cada
/// ingrediente coincidente con la alergia concreta que lo dispara y su severidad. Deuda técnica:
/// reemplazar por la tabla formal <c>allergy_food_items</c> (acta A26).
/// </summary>
public sealed class CustomFoodAllergenChecker : ICustomFoodAllergenChecker
{
    private readonly CauceDbContext _context;
    private readonly AllergyHeuristicMatcher _matcher;

    /// <summary>
    /// Inicializa el checker con sus dependencias.
    /// </summary>
    /// <param name="context">Contexto de Entity Framework Core.</param>
    /// <param name="matcher">Matcher heurístico de alergias.</param>
    public CustomFoodAllergenChecker(CauceDbContext context, AllergyHeuristicMatcher matcher)
    {
        _context = context;
        _matcher = matcher;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DetectedAllergen>> CheckAsync(
        Guid patientId,
        IReadOnlyCollection<Guid> ingredientFoodIds,
        CancellationToken ct = default)
    {
        if (ingredientFoodIds.Count == 0)
        {
            return [];
        }

        var declarations = await (from declaration in _context.Set<PatientAllergy>().AsNoTracking()
                                  where declaration.PatientId == patientId
                                  join allergy in _context.Set<Allergy>().AsNoTracking()
                                      on declaration.AllergyId equals allergy.Id
                                  select new { allergy.Name, declaration.Severity })
            .ToListAsync(ct)
            .ConfigureAwait(false);
        if (declarations.Count == 0)
        {
            return [];
        }

        var foods = await _context.Set<FoodItem>()
            .AsNoTracking()
            .Where(food => ingredientFoodIds.Contains(food.Id))
            .Select(food => new FoodCatalogEntry(food.Id, food.Name, food.Category, food.FodmapTags))
            .ToListAsync(ct)
            .ConfigureAwait(false);
        if (foods.Count == 0)
        {
            return [];
        }

        var detected = new List<DetectedAllergen>();
        foreach (var declaration in declarations)
        {
            var forbidden = _matcher.GetForbiddenFoodIds([declaration.Name], foods);
            foreach (var food in foods.Where(food => forbidden.Contains(food.Id)))
            {
                detected.Add(new DetectedAllergen(food.Name, declaration.Name, declaration.Severity.ToString()));
            }
        }

        return detected;
    }
}
