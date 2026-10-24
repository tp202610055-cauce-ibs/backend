using Cauce.Infrastructure.Persistence;
using Cauce.Infrastructure.Qa.Manifest;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Infrastructure.Qa;

/// <summary>
/// Alimentos y alergias activos del catálogo, por nombre exacto sin distinguir mayúsculas.
/// </summary>
public sealed class QaCatalog
{
    /// <summary>
    /// Alimento que usa una recomendación del motor que no lista ninguno.
    /// </summary>
    public const string DefaultReduceFood = "Cebolla cocida";

    /// <summary>
    /// Alimento de las comidas que reparte una siembra de actividad; es bajo en FODMAP para no sesgar los datos de apoyo.
    /// </summary>
    public const string DefaultActivityFood = "Arroz blanco cocido";

    /// <summary>
    /// Crea el catálogo a partir de los pares nombre e identificador.
    /// </summary>
    public QaCatalog(IReadOnlyDictionary<string, Guid> foods, IReadOnlyDictionary<string, Guid> allergies)
    {
        Foods = new Dictionary<string, Guid>(foods, StringComparer.OrdinalIgnoreCase);
        Allergies = new Dictionary<string, Guid>(allergies, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Identificador de cada alimento activo, por nombre.
    /// </summary>
    public IReadOnlyDictionary<string, Guid> Foods { get; }

    /// <summary>
    /// Identificador de cada alergia activa, por nombre.
    /// </summary>
    public IReadOnlyDictionary<string, Guid> Allergies { get; }

    /// <summary>
    /// Lee el catálogo activo de la base de datos.
    /// </summary>
    public static async Task<QaCatalog> LoadAsync(CauceDbContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        var foods = await context.FoodItems.AsNoTracking()
            .Where(food => food.IsActive)
            .Select(food => new { food.Name, food.Id })
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var allergies = await context.Allergies.AsNoTracking()
            .Where(allergy => allergy.IsActive)
            .Select(allergy => new { allergy.Name, allergy.Id })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return new QaCatalog(
            foods.GroupBy(food => food.Name, StringComparer.OrdinalIgnoreCase).ToDictionary(group => group.Key, group => group.First().Id),
            allergies.GroupBy(allergy => allergy.Name, StringComparer.OrdinalIgnoreCase).ToDictionary(group => group.Key, group => group.First().Id));
    }

    /// <summary>
    /// Lista todo lo que el manifiesto referencia y el catálogo no tiene. Vacía si no falta nada.
    /// </summary>
    public IReadOnlyList<string> FindMissing(QaManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var missingFoods = RequiredFoods(manifest).Where(name => !Foods.ContainsKey(name));
        var missingAllergies = RequiredAllergies(manifest).Where(name => !Allergies.ContainsKey(name));

        return missingFoods.Select(name => $"Alimento sin catálogo: «{name}».")
            .Concat(missingAllergies.Select(name => $"Alergia sin catálogo: «{name}»."))
            .ToList();
    }

    /// <summary>
    /// Alimentos que la siembra necesita, sin repetir.
    /// </summary>
    public static IReadOnlyList<string> RequiredFoods(QaManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var names = new List<string>();
        foreach (var seed in manifest.Cases.SelectMany(testCase => testCase.Seeds))
        {
            switch (seed)
            {
                case QaMealSeed meal:
                    names.Add(meal.Food);
                    break;
                case QaCustomFoodSeed customFood:
                    names.AddRange(customFood.Ingredients);
                    break;
                case QaRecommendationSeed { IsManual: false } recommendation:
                    names.AddRange(EngineFoods(recommendation).Select(item => item.Food));
                    break;
                case QaActivitySeed:
                    names.Add(DefaultActivityFood);
                    break;
            }
        }

        return names.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Alergias que la siembra necesita, sin repetir.
    /// </summary>
    public static IReadOnlyList<string> RequiredAllergies(QaManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        return manifest.Cases
            .SelectMany(testCase => testCase.Seeds)
            .OfType<QaProfileSeed>()
            .SelectMany(profile => profile.Allergies)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Alimentos de una recomendación del motor con su acción; si no lista ninguno, reduce el alimento por omisión.
    /// </summary>
    public static IReadOnlyList<(string Food, QaItemAction Action)> EngineFoods(QaRecommendationSeed recommendation)
    {
        ArgumentNullException.ThrowIfNull(recommendation);
        var items = recommendation.Suggests.Select(food => (food, QaItemAction.Suggest))
            .Concat(recommendation.Reduce.Select(food => (food, QaItemAction.Reduce)))
            .Concat(recommendation.Avoid.Select(food => (food, QaItemAction.Avoid)))
            .ToList();

        return items.Count > 0 ? items : [(DefaultReduceFood, QaItemAction.Reduce)];
    }
}

/// <summary>
/// Acción de un ítem de recomendación del motor, en el vocabulario del manifiesto.
/// </summary>
public enum QaItemAction
{
    Suggest,
    Reduce,
    Avoid
}
