using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry.Enums;
using Cauce.Infrastructure.Qa.Manifest;

namespace Cauce.Infrastructure.Qa.Seeding;

/// <summary>
/// Comidas, platos propios, síntomas y actividad de registro del paciente, con las fábricas del dominio y fechados
/// desde el ancla. Los síntomas no se asocian a comidas: esa asociación la calcula el servidor al registrarlos.
/// </summary>
public sealed class QaClinicalRecordSeedApplier : IQaSeedApplier
{
    /// <summary>
    /// Gramos de una comida o de la porción de un plato propio cuando el caso no los da.
    /// </summary>
    public const decimal DefaultGrams = 150m;

    /// <summary>
    /// Intensidad de un síntoma cuando el caso no la da (escala de 1 a 100).
    /// </summary>
    public const int DefaultIntensity = 50;

    private static readonly IReadOnlyDictionary<string, MealTime> Slots = new Dictionary<string, MealTime>(StringComparer.OrdinalIgnoreCase)
    {
        ["desayuno"] = MealTime.Breakfast,
        ["almuerzo"] = MealTime.Lunch,
        ["cena"] = MealTime.Dinner,
        ["merienda"] = MealTime.Snack,
        ["snack"] = MealTime.Snack
    };

    /// <inheritdoc />
    public int Order => 30;

    /// <inheritdoc />
    public bool CanApply(QaSeed seed) => seed is QaMealSeed or QaCustomFoodSeed or QaSymptomSeed or QaActivitySeed;

    /// <inheritdoc />
    public Task ApplyAsync(QaSeed seed, QaSeedContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        switch (seed)
        {
            case QaMealSeed meal:
                var slot = meal.Slot is not null && Slots.TryGetValue(meal.Slot, out var parsed) ? parsed : MealTime.Lunch;
                AddMeal(context, meal.Food, context.Clock.Resolve(meal.Ago), meal.Grams ?? DefaultGrams, slot);
                break;
            case QaCustomFoodSeed customFood:
                AddCustomFood(context, customFood);
                break;
            case QaSymptomSeed symptom:
                AddSymptom(context, symptom.Name, symptom.Intensity ?? DefaultIntensity, context.Clock.Resolve(symptom.Ago));
                break;
            case QaActivitySeed activity:
                foreach (var day in ActivityDays(context.Clock, activity))
                {
                    AddMeal(context, QaCatalog.DefaultActivityFood, day, DefaultGrams, MealTime.Lunch);
                }

                break;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Registra una comida de un alimento del catálogo, sin guardar.
    /// </summary>
    public static void AddMeal(QaSeedContext context, string food, DateTime consumedAt, decimal grams, MealTime slot)
    {
        ArgumentNullException.ThrowIfNull(context);
        var item = new MealItemInput(context.Catalog.Foods[food], null, grams, MeasurementUnit.Grams);
        context.Db.Meals.Add(Meal.Register(
            Guid.NewGuid(), Guid.NewGuid(), context.Owner.Id, slot, consumedAt, consumedAt, [item], context.Clock.Anchor));
    }

    /// <summary>
    /// Registra un síntoma por su etiqueta en español, sin guardar.
    /// </summary>
    public static void AddSymptom(QaSeedContext context, string name, int intensity, DateTime occurredAt)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!QaManifestValidator.TryParseSymptom(name, out var type))
        {
            throw new InvalidOperationException($"{context.Cp}: síntoma desconocido «{name}».");
        }

        context.Db.Symptoms.Add(Symptom.Report(
            Guid.NewGuid(), Guid.NewGuid(), context.Owner.Id, type, intensity, occurredAt, occurredAt, context.Clock.Anchor));
    }

    /// <summary>
    /// Días con registro de una siembra de actividad, repartidos de forma pareja y siempre distintos.
    /// </summary>
    public static IReadOnlyList<DateTime> ActivityDays(QaClock clock, QaActivitySeed activity)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(activity);

        if (activity.DaysWithRecordsLast14 is { } recent)
        {
            return Enumerable.Range(0, recent)
                .Select(index => index * 14 / recent)
                .Select(daysAgo => clock.Anchor.AddDays(-daysAgo))
                .ToList();
        }

        QaManifestValidator.TryParseMonth(activity.Month, out var firstDay);
        var monthDays = DateTime.DaysInMonth(firstDay.Year, firstDay.Month);
        var count = Math.Min(activity.DaysWithRecordsInMonth ?? 0, monthDays);
        return Enumerable.Range(0, count)
            .Select(index => firstDay.AddDays(index * monthDays / count))
            .Select(day => QaClock.AtLimaTime(day, new TimeOnly(13, 0)))
            .ToList();
    }

    private static void AddCustomFood(QaSeedContext context, QaCustomFoodSeed seed)
    {
        var customFood = CustomFood.Create(Guid.NewGuid(), context.Owner.Id, seed.Name, DefaultGrams, context.Clock.Anchor);
        var share = seed.Ingredients.Count == 0 ? DefaultGrams : Math.Round(DefaultGrams / seed.Ingredients.Count, 2);
        foreach (var ingredient in seed.Ingredients)
        {
            customFood.AddIngredient(context.Catalog.Foods[ingredient], share);
        }

        context.Db.CustomFoods.Add(customFood);
    }
}
