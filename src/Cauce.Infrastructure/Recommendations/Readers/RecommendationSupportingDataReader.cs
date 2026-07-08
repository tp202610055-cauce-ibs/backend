using Cauce.Application.Common.Interfaces.Recommendations;
using Cauce.Domain.ClinicalRegistry.Enums;
using Cauce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Infrastructure.Recommendations.Readers;

/// <summary>
/// Implementación de <see cref="IRecommendationSupportingDataReader"/> con consultas deterministas
/// sobre <see cref="CauceDbContext"/>: conteo de síntomas y comidas y ranking de alimentos FODMAP alto
/// consumidos en la ventana de análisis (US15 CA03, bloque 4).
/// </summary>
public sealed class RecommendationSupportingDataReader : IRecommendationSupportingDataReader
{
    private const int TopFoodsCount = 3;

    private readonly CauceDbContext _context;

    /// <summary>
    /// Inicializa el lector con el contexto de base de datos.
    /// </summary>
    /// <param name="context">Contexto de Entity Framework Core.</param>
    public RecommendationSupportingDataReader(CauceDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<RecommendationSupportingDataSnapshot> GetAsync(
        Guid patientId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken ct = default)
    {
        var symptomCount = await _context.Symptoms
            .AsNoTracking()
            .CountAsync(symptom => symptom.PatientId == patientId
                && symptom.OccurredAt >= fromUtc && symptom.OccurredAt < toUtc, ct)
            .ConfigureAwait(false);

        var mealCount = await _context.Meals
            .AsNoTracking()
            .CountAsync(meal => meal.PatientId == patientId
                && meal.ConsumedAt >= fromUtc && meal.ConsumedAt < toUtc, ct)
            .ConfigureAwait(false);

        var topHighFodmapFoods = await (
            from meal in _context.Meals.AsNoTracking()
            where meal.PatientId == patientId && meal.ConsumedAt >= fromUtc && meal.ConsumedAt < toUtc
            join item in _context.MealItems.AsNoTracking() on meal.Id equals item.MealId
            from food in _context.FoodItems.AsNoTracking()
                .Where(candidate => candidate.Id == item.FoodId && candidate.FodmapLevel == FodmapLevel.High)
            group food by food.Name into grouped
            orderby grouped.Count() descending, grouped.Key
            select grouped.Key)
            .Take(TopFoodsCount)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return new RecommendationSupportingDataSnapshot(symptomCount, mealCount, topHighFodmapFoods);
    }
}
