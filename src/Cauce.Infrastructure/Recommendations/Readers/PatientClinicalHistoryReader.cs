using Cauce.Application.Common.Interfaces.Recommendations;
using Cauce.Application.Recommendations.Contracts;
using Cauce.Domain.ClinicalRegistry;
using Cauce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Infrastructure.Recommendations.Readers;

/// <summary>
/// Implementación de <see cref="IPatientClinicalHistoryReader"/> mediante consultas LINQ tipadas
/// sobre las tablas de comidas y del catálogo. Solo considera ítems que referencian un alimento
/// del catálogo (no personalizados) y activos. La cantidad se aproxima con el promedio de la
/// cantidad registrada, sin convertir unidades (ver DEC-B4-03).
/// </summary>
public sealed class PatientClinicalHistoryReader : IPatientClinicalHistoryReader
{
    private readonly CauceDbContext _context;

    /// <summary>
    /// Inicializa el lector con el contexto de base de datos.
    /// </summary>
    /// <param name="context">Contexto de Entity Framework Core.</param>
    public PatientClinicalHistoryReader(CauceDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CandidateFood>> GetCandidateFoodsAsync(
        Guid patientId,
        int windowDays,
        CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-windowDays);

        var consumption = await _context.Set<MealItem>()
            .Where(item => item.FoodId != null)
            .Join(
                _context.Set<Meal>().Where(meal => meal.PatientId == patientId && meal.ConsumedAt >= cutoff),
                item => item.MealId,
                meal => meal.Id,
                (item, meal) => item)
            .GroupBy(item => item.FoodId!.Value)
            .Select(group => new { FoodId = group.Key, AverageQuantity = group.Average(item => item.Quantity) })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (consumption.Count == 0)
        {
            return Array.Empty<CandidateFood>();
        }

        var foodIds = consumption.Select(entry => entry.FoodId).ToList();
        var foods = await _context.Set<FoodItem>()
            .AsNoTracking()
            .Where(food => foodIds.Contains(food.Id) && food.IsActive)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var averageByFood = consumption.ToDictionary(entry => entry.FoodId, entry => entry.AverageQuantity);

        return foods
            .Select(food => new CandidateFood(
                food.Id,
                food.Name,
                food.Category,
                food.FodmapLevel,
                food.OligosLevel,
                food.FructoseLevel,
                food.PolyolsLevel,
                food.LactoseLevel,
                (int)Math.Round(averageByFood[food.Id], MidpointRounding.AwayFromZero)))
            .ToList();
    }
}
