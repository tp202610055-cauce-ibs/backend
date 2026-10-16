using System.Security.Cryptography;
using System.Text;
using Cauce.Domain.ClinicalRegistry.Enums;

namespace Cauce.Infrastructure.Persistence.Seeders;

/// <summary>
/// Genera, de forma pura y determinista, el historial de volumen del paciente de desarrollo: 450 comidas y
/// 180 síntomas en los 88 días anteriores a hoy (hora de Lima, UTC-5 fijo) más unas filas de hoy.
/// </summary>
public static class VolumeHistoryGenerator
{
    /// <summary>
    /// Cantidad total de comidas planificadas.
    /// </summary>
    public const int MealCount = 450;

    /// <summary>
    /// Cantidad total de síntomas planificados.
    /// </summary>
    public const int SymptomCount = 180;

    /// <summary>
    /// Días hacia atrás que cubre el historial, sin contar hoy.
    /// </summary>
    public const int HistoryDays = 88;

    /// <summary>
    /// Tamaño de página con el que se calculan los cortes de los grupos empatados.
    /// </summary>
    public const int PageSize = 100;

    /// <summary>
    /// Registros de cada grupo empatado sobre un corte de página.
    /// </summary>
    public const int TieGroupSize = 6;

    /// <summary>
    /// Semilla fija del sembrado.
    /// </summary>
    public const int DefaultSeed = 20261015;

    /// <summary>
    /// Nombre del alimento de prueba, que ninguna comida sembrada usa.
    /// </summary>
    public const string ProbeFoodName = "Alimento de prueba retirado";

    /// <summary>
    /// Desfase fijo de la hora de Lima respecto de UTC.
    /// </summary>
    public static readonly TimeSpan LimaOffset = TimeSpan.FromHours(-5);

    /// <summary>
    /// Hora de Lima desde la que se puede sembrar: antes, una fila de «hoy» podría caer en ayer.
    /// </summary>
    public static readonly TimeSpan EarliestSeedingTime = new(0, 30, 0);

    private const int EmptyDays = 5;
    private const int BusyDays = 3;
    private const int BusyDayMeals = 8;
    private const int CrossMidnightDays = 5;
    private const int LateMeals = 26;
    private const int CustomOnlyMeals = 3;
    private const int CustomWithCatalogMeals = 33;
    private const int SixItemMeals = 5;
    private const int UnassociatedSymptoms = 63;
    private const double PeruvianPreference = 0.62;

    private static readonly TimeSpan AssociationWindow = TimeSpan.FromHours(4);

    private static readonly (string Name, PlannedIngredient[] Ingredients)[] CustomFoodRecipes =
    [
        ("Sopa de quinua casera", [new("Quinua blanca cocida", 120m), new("Zanahoria cocida", 60m), new("Pollo a la plancha", 80m)]),
        ("Ensalada de papa y huevo", [new("Papa amarilla cocida", 150m), new("Huevo de gallina cocido", 60m)]),
        ("Batido de papaya", [new("Papaya", 200m), new("Yogur natural sin lactosa", 150m)])
    ];

    private static readonly (SymptomType Type, int Weight)[] SymptomWeights =
    [
        (SymptomType.AbdominalPain, 24), (SymptomType.Bloating, 22), (SymptomType.Flatulence, 12),
        (SymptomType.Diarrhea, 10), (SymptomType.Constipation, 10), (SymptomType.Urgency, 8),
        (SymptomType.Nausea, 5), (SymptomType.Reflux, 5), (SymptomType.Other, 4)
    ];

    private enum Slot
    {
        Breakfast,
        Lunch,
        Dinner,
        MorningSnack,
        AfternoonSnack,
        NightSnack,
        CrossMidnight,
        Today
    }

    /// <summary>
    /// Convierte un instante UTC a la hora local de Lima.
    /// </summary>
    public static DateTime ToLima(DateTime utc) => DateTime.SpecifyKind(utc + LimaOffset, DateTimeKind.Unspecified);

    /// <summary>
    /// Convierte una hora local de Lima a UTC.
    /// </summary>
    public static DateTime FromLima(DateTime lima) => DateTime.SpecifyKind(lima - LimaOffset, DateTimeKind.Utc);

    /// <summary>
    /// Indica si a esa hora se puede sembrar: en Lima deben ser las 00:30 o más.
    /// </summary>
    public static bool CanSeedAt(DateTime utcNow) => ToLima(utcNow).TimeOfDay >= EarliestSeedingTime;

    /// <summary>
    /// Compara dos registros en el orden del servidor: <c>clientCreatedAt</c> descendente y luego id
    /// descendente, con los uuid comparados byte a byte como lo hace PostgreSQL.
    /// </summary>
    public static int CompareServerOrder(DateTime firstCreatedAt, Guid firstId, DateTime secondCreatedAt, Guid secondId)
    {
        var byTime = secondCreatedAt.CompareTo(firstCreatedAt);
        return byTime != 0
            ? byTime
            : secondId.ToByteArray(bigEndian: true).AsSpan().SequenceCompareTo(firstId.ToByteArray(bigEndian: true));
    }

    /// <summary>
    /// Deriva un identificador estable a partir del paciente, el tipo de registro y su índice.
    /// </summary>
    public static Guid DeriveId(Guid patientId, string kind, int index)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{patientId:N}:{kind}:{index}"));
        var bytes = hash.AsSpan(0, 16).ToArray();
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x80);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes, bigEndian: true);
    }

    /// <summary>
    /// Genera el plan completo. Mismo instante, paciente, catálogo y semilla producen el mismo plan.
    /// </summary>
    /// <param name="utcNow">Instante de la siembra, en UTC.</param>
    /// <param name="patientId">Identificador del paciente.</param>
    /// <param name="catalog">Alimentos activos del catálogo.</param>
    /// <param name="seed">Semilla del generador.</param>
    /// <returns>El plan del historial.</returns>
    /// <exception cref="InvalidOperationException">Si son menos de las 00:30 en Lima o falta un alimento.</exception>
    public static VolumeHistoryPlan Generate(
        DateTime utcNow,
        Guid patientId,
        IReadOnlyList<CatalogFood> catalog,
        int seed = DefaultSeed)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (!CanSeedAt(utcNow))
        {
            throw new InvalidOperationException("No se siembra antes de las 00:30 de Lima.");
        }

        var now = new DateTime(utcNow.Ticks - (utcNow.Ticks % TimeSpan.TicksPerSecond), DateTimeKind.Utc);
        var random = new Random(seed);
        // Ordenado por nombre: el plan no depende del orden en que la base devuelva el catálogo.
        var picker = new FoodPicker(
            catalog.Where(food => food.Name != ProbeFoodName).OrderBy(food => food.Name, StringComparer.Ordinal).ToList(),
            random);
        var customFoods = BuildCustomFoods(patientId, picker);

        var meals = BuildMealTimes(now, random);
        if (meals.Count != MealCount)
        {
            throw new InvalidOperationException($"El plan de comidas tiene {meals.Count} filas y deben ser {MealCount}.");
        }

        AssignItems(meals, picker, random);
        var mealTieGroups = ApplyTieGroups(meals, "meals", patientId, "meal", MealCount / PageSize);

        var symptoms = BuildSymptomTimes(now, meals, random);
        AssignSymptomDetails(symptoms, random);
        var symptomTieGroups = ApplyTieGroups(symptoms, "symptoms", patientId, "symptom", SymptomCount / PageSize);

        var plannedMeals = meals
            .Select((draft, index) => new PlannedMeal(
                DeriveId(patientId, "meal", index),
                DeriveId(patientId, "meal-client", index),
                draft.MealTime,
                draft.ConsumedAt,
                draft.ClientCreatedAt,
                draft.Items))
            .ToList();

        var plannedSymptoms = symptoms
            .Select((draft, index) => new PlannedSymptom(
                DeriveId(patientId, "symptom", index),
                DeriveId(patientId, "symptom-client", index),
                draft.Type,
                draft.Intensity,
                draft.OccurredAt,
                draft.ClientCreatedAt,
                FindAssociatedMeal(plannedMeals, draft.ClientCreatedAt)))
            .ToList();

        return new VolumeHistoryPlan(
            now, customFoods, plannedMeals, plannedSymptoms, mealTieGroups.Concat(symptomTieGroups).ToList());
    }

    /// <summary>
    /// Aplica la regla del registro de síntomas: la comida más reciente con <c>clientCreatedAt</c> en
    /// (instante del síntoma menos 4 horas, instante del síntoma].
    /// </summary>
    public static Guid? FindAssociatedMeal(IReadOnlyList<PlannedMeal> meals, DateTime symptomClientCreatedAt)
    {
        ArgumentNullException.ThrowIfNull(meals);
        var windowStart = symptomClientCreatedAt - AssociationWindow;
        PlannedMeal? latest = null;
        foreach (var meal in meals)
        {
            if (meal.ClientCreatedAt <= windowStart || meal.ClientCreatedAt > symptomClientCreatedAt)
            {
                continue;
            }

            if (latest is null || CompareServerOrder(meal.ClientCreatedAt, meal.Id, latest.ClientCreatedAt, latest.Id) < 0)
            {
                latest = meal;
            }
        }

        return latest?.Id;
    }

    private static List<PlannedCustomFood> BuildCustomFoods(Guid patientId, FoodPicker picker)
    {
        return CustomFoodRecipes
            .Select((recipe, index) =>
            {
                foreach (var ingredient in recipe.Ingredients)
                {
                    picker.Require(ingredient.FoodName);
                }

                return new PlannedCustomFood(DeriveId(patientId, "custom-food", index), recipe.Name, recipe.Ingredients);
            })
            .ToList();
    }

    private static List<MealDraft> BuildMealTimes(DateTime now, Random random)
    {
        var limaToday = ToLima(now).Date;
        var counts = PlanDailyCounts(random);
        var crossDays = Enumerable.Range(2, HistoryDays - 1)
            .Where(day => counts[day] >= 4)
            .OrderBy(_ => random.Next())
            .Take(CrossMidnightDays)
            .ToHashSet();

        var meals = new List<MealDraft>
        {
            new(Slot.Today, now.AddMinutes(-25), now.AddMinutes(-22)),
            new(Slot.Today, now.AddMinutes(-12), now.AddMinutes(-10)),
            new(Slot.CrossMidnight, FromLima(limaToday.AddDays(-1).AddHours(23).AddMinutes(40)), FromLima(limaToday.AddMinutes(10)))
        };

        for (var day = 1; day <= HistoryDays; day++)
        {
            if (counts[day] == 0)
            {
                continue;
            }

            var date = limaToday.AddDays(-day);
            var slots = PlanSlots(counts[day] - (day == 1 ? 1 : 0), day == 1 || crossDays.Contains(day), day == 1, random);
            var usedMinutes = new HashSet<int>();
            foreach (var slot in slots)
            {
                var minute = PickMinute(slot, random, usedMinutes);
                var consumedLima = date.AddMinutes(minute).AddSeconds(random.Next(0, 60));
                var delay = slot == Slot.CrossMidnight
                    ? TimeSpan.FromMinutes((24 * 60) - minute + random.Next(5, 36))
                    : TimeSpan.FromMinutes(random.Next(0, 41)).Add(TimeSpan.FromSeconds(random.Next(0, 60)));
                var consumedAt = FromLima(consumedLima);
                meals.Add(new MealDraft(slot, consumedAt, consumedAt + delay));
            }
        }

        // Una parte se registra entre 10 y 14 horas después del consumo.
        var lateCandidates = Enumerable.Range(0, meals.Count)
            .Where(index => meals[index].Slot is Slot.Breakfast or Slot.Lunch or Slot.MorningSnack or Slot.AfternoonSnack
                && ToLima(meals[index].ConsumedAt).Date <= limaToday.AddDays(-2))
            .OrderBy(_ => random.Next())
            .Take(LateMeals);
        foreach (var index in lateCandidates)
        {
            meals[index].ClientCreatedAt = meals[index].ConsumedAt
                + TimeSpan.FromMinutes(random.Next(10 * 60, (14 * 60) + 1)).Add(TimeSpan.FromSeconds(random.Next(0, 60)));
        }

        MakeUnique(meals, now);
        foreach (var meal in meals)
        {
            meal.MealTime = MealTimeFor(ToLima(meal.ConsumedAt).TimeOfDay);
        }

        return meals;
    }

    private static int[] PlanDailyCounts(Random random)
    {
        var counts = new int[HistoryDays + 1];
        var empty = new HashSet<int>();
        var runStart = random.Next(20, 61);
        empty.UnionWith([runStart, runStart + 1, runStart + 2]);
        while (empty.Count < EmptyDays)
        {
            var day = random.Next(5, HistoryDays + 1);
            if (!empty.Contains(day) && !empty.Contains(day - 1) && !empty.Contains(day + 1))
            {
                empty.Add(day);
            }
        }

        var available = Enumerable.Range(2, HistoryDays - 1).Where(day => !empty.Contains(day)).ToList();
        var busy = available.OrderBy(_ => random.Next()).Take(BusyDays).ToHashSet();
        var regular = Enumerable.Range(1, HistoryDays).Where(day => !empty.Contains(day) && !busy.Contains(day)).ToList();

        foreach (var day in busy)
        {
            counts[day] = BusyDayMeals;
        }

        foreach (var day in regular)
        {
            counts[day] = 5;
        }

        var remaining = MealCount - 2 - counts.Sum();
        while (remaining > 0)
        {
            var day = regular[random.Next(regular.Count)];
            if (counts[day] < 7)
            {
                counts[day]++;
                remaining--;
            }
        }

        for (var swap = 0; swap < 60; swap++)
        {
            var from = regular[random.Next(regular.Count)];
            var to = regular[random.Next(regular.Count)];
            if (from != to && counts[from] > 3 && counts[to] < 7 && !(from == 1 && counts[from] <= 4))
            {
                counts[from]--;
                counts[to]++;
            }
        }

        return counts;
    }

    private static List<Slot> PlanSlots(int count, bool crossMidnight, bool crossMidnightAlreadyPlanned, Random random)
    {
        var slots = new List<Slot> { Slot.Breakfast, Slot.Lunch, Slot.Dinner };
        var extras = count - slots.Count;
        if (crossMidnight && !crossMidnightAlreadyPlanned && extras > 0)
        {
            slots.Add(Slot.CrossMidnight);
            extras--;
        }

        var snacks = new List<Slot> { Slot.MorningSnack, Slot.AfternoonSnack, Slot.MorningSnack, Slot.AfternoonSnack }
            .OrderBy(_ => random.Next())
            .ToList();
        if (extras > snacks.Count || (extras > 0 && !crossMidnight && random.NextDouble() < 0.15))
        {
            slots.Add(Slot.NightSnack);
            extras--;
        }

        slots.AddRange(snacks.Take(extras));
        return slots;
    }

    private static int PickMinute(Slot slot, Random random, HashSet<int> usedMinutes)
    {
        var (from, to) = slot switch
        {
            Slot.Breakfast => (6 * 60 + 30, 9 * 60 + 25),
            Slot.MorningSnack => (9 * 60 + 45, 11 * 60 + 50),
            Slot.Lunch => (12 * 60, 14 * 60 + 55),
            Slot.AfternoonSnack => (15 * 60 + 15, 18 * 60 + 20),
            Slot.Dinner => (18 * 60 + 30, 22 * 60 + 25),
            Slot.NightSnack => (22 * 60 + 40, 23 * 60 + 5),
            _ => (23 * 60 + 15, 23 * 60 + 50)
        };

        int minute;
        do
        {
            minute = random.Next(from, to + 1);
        }
        while (!usedMinutes.Add(minute));

        return minute;
    }

    private static MealTime MealTimeFor(TimeSpan limaTime)
    {
        var minutes = (int)limaTime.TotalMinutes;
        return minutes switch
        {
            >= 6 * 60 + 30 and <= 9 * 60 + 30 => MealTime.Breakfast,
            >= 12 * 60 and <= 15 * 60 => MealTime.Lunch,
            >= 18 * 60 + 30 and <= 22 * 60 + 30 => MealTime.Dinner,
            _ => MealTime.Snack
        };
    }

    private static void AssignItems(List<MealDraft> meals, FoodPicker picker, Random random)
    {
        var levels = Enumerable.Repeat(FodmapLevel.Low, 270)
            .Concat(Enumerable.Repeat(FodmapLevel.Moderate, 113))
            .Concat(Enumerable.Repeat(FodmapLevel.High, MealCount - 270 - 113))
            .OrderBy(_ => random.Next())
            .ToList();

        var order = Enumerable.Range(0, meals.Count).OrderBy(_ => random.Next()).ToList();
        var customOnly = order.Where(index => levels[index] == FodmapLevel.Low).Take(CustomOnlyMeals).ToHashSet();
        var withCustom = order.Where(index => !customOnly.Contains(index)).Take(CustomWithCatalogMeals).ToHashSet();
        var sixItems = order.Where(index => !customOnly.Contains(index) && !withCustom.Contains(index)).Skip(100).Take(SixItemMeals).ToHashSet();

        for (var index = 0; index < meals.Count; index++)
        {
            var items = new List<PlannedMealItem>();
            if (customOnly.Contains(index))
            {
                items.Add(CustomItem(random));
                meals[index].Items = items;
                continue;
            }

            var catalogItems = sixItems.Contains(index) ? 6 : PickItemCount(random);
            if (withCustom.Contains(index))
            {
                items.Add(CustomItem(random));
                catalogItems = Math.Min(catalogItems, 3);
            }

            var used = new HashSet<string>();
            for (var position = 0; position < catalogItems; position++)
            {
                var level = (levels[index], position) switch
                {
                    (FodmapLevel.Low, _) => FodmapLevel.Low,
                    (var target, 0) => target,
                    (FodmapLevel.High, _) when random.NextDouble() < 0.3 => FodmapLevel.Moderate,
                    _ => FodmapLevel.Low
                };
                var food = picker.Pick(level, used);
                used.Add(food);
                items.Add(CatalogItem(food, random));
            }

            meals[index].Items = items;
        }
    }

    private static int PickItemCount(Random random)
    {
        var roll = random.NextDouble();
        return roll switch
        {
            < 0.15 => 1,
            < 0.50 => 2,
            < 0.85 => 3,
            _ => 4
        };
    }

    private static PlannedMealItem CustomItem(Random random)
    {
        return new PlannedMealItem(null, random.Next(CustomFoodRecipes.Length), PickHalfStep(random), MeasurementUnit.Units);
    }

    private static PlannedMealItem CatalogItem(string foodName, Random random)
    {
        var roll = random.NextDouble();
        return roll switch
        {
            < 0.60 => new PlannedMealItem(foodName, null, random.Next(3, 26) * 10m, MeasurementUnit.Grams),
            < 0.70 => new PlannedMealItem(foodName, null, PickHalfStep(random), MeasurementUnit.Cups),
            < 0.82 => new PlannedMealItem(foodName, null, PickHalfStep(random), MeasurementUnit.Units),
            < 0.92 => new PlannedMealItem(foodName, null, PickHalfStep(random), MeasurementUnit.Tablespoons),
            _ => new PlannedMealItem(foodName, null, new[] { 2m, 3m, 4m, 6m, 8m }[random.Next(5)], MeasurementUnit.Ounces)
        };
    }

    private static decimal PickHalfStep(Random random) => new[] { 0.5m, 1m, 1.5m, 2m }[random.Next(4)];

    private static List<SymptomDraft> BuildSymptomTimes(DateTime now, List<MealDraft> meals, Random random)
    {
        var limaToday = ToLima(now).Date;
        var mealDays = meals
            .Where(meal => meal.Slot != Slot.Today)
            .GroupBy(meal => ToLima(meal.ConsumedAt).Date)
            .Where(group => group.Key < limaToday)
            .ToDictionary(group => group.Key, group => group.ToList());
        var days = mealDays.Keys.OrderBy(day => day).ToList();

        var perDay = days.ToDictionary(day => day, _ => 2);
        var extra = SymptomCount - 1 - (days.Count * 2);
        foreach (var day in days.OrderBy(_ => random.Next()).Take(extra))
        {
            perDay[day]++;
        }

        var slots = perDay.SelectMany(pair => Enumerable.Repeat(pair.Key, pair.Value)).ToList();
        var unassociated = Enumerable.Range(0, slots.Count).OrderBy(_ => random.Next()).Take(UnassociatedSymptoms).ToHashSet();
        var mealCreatedAt = meals.Select(meal => meal.ClientCreatedAt).ToList();

        var symptoms = new List<SymptomDraft> { new(now.AddMinutes(-8), now.AddMinutes(-7)) };
        for (var index = 0; index < slots.Count; index++)
        {
            var day = slots[index];
            symptoms.Add(unassociated.Contains(index)
                ? PlaceEarlyMorning(day, mealCreatedAt, symptoms, random)
                : PlaceAfterMeal(mealDays[day], random));
        }

        MakeUnique(symptoms, now);
        return symptoms;
    }

    private static SymptomDraft PlaceAfterMeal(List<MealDraft> dayMeals, Random random)
    {
        var candidates = dayMeals
            .Where(meal => meal.Slot is Slot.Breakfast or Slot.Lunch or Slot.MorningSnack or Slot.AfternoonSnack
                && meal.ClientCreatedAt - meal.ConsumedAt <= TimeSpan.FromHours(1))
            .ToList();
        if (candidates.Count == 0)
        {
            candidates = [dayMeals.OrderBy(meal => meal.ClientCreatedAt - meal.ConsumedAt).First()];
        }

        var meal = candidates[random.Next(candidates.Count)];
        var createdAt = meal.ClientCreatedAt + TimeSpan.FromMinutes(random.Next(15, 211)) + TimeSpan.FromSeconds(random.Next(0, 60));
        var occurredAt = createdAt - TimeSpan.FromMinutes(random.Next(0, 26));
        return new SymptomDraft(occurredAt, createdAt);
    }

    private static SymptomDraft PlaceEarlyMorning(DateTime day, List<DateTime> mealCreatedAt, List<SymptomDraft> placed, Random random)
    {
        var start = random.Next(0, 6) * 5;
        for (var minute = (4 * 60) + 5 + start; minute <= (6 * 60) + 20; minute += 5)
        {
            var occurredAt = FromLima(day.AddMinutes(minute).AddSeconds(random.Next(0, 60)));
            var createdAt = occurredAt + TimeSpan.FromMinutes(random.Next(0, 16));
            var windowStart = createdAt - AssociationWindow;
            if (!mealCreatedAt.Any(meal => meal > windowStart && meal <= createdAt)
                && !placed.Any(symptom => symptom.ClientCreatedAt == createdAt))
            {
                return new SymptomDraft(occurredAt, createdAt);
            }
        }

        // Sin un hueco de madrugada libre de comidas, se acepta a las 04:00 aunque quede asociado.
        var fallback = FromLima(day.AddHours(4));
        return new SymptomDraft(fallback, fallback.AddMinutes(1));
    }

    private static void AssignSymptomDetails(List<SymptomDraft> symptoms, Random random)
    {
        var totalWeight = SymptomWeights.Sum(entry => entry.Weight);
        foreach (var symptom in symptoms)
        {
            var roll = random.Next(totalWeight);
            foreach (var (type, weight) in SymptomWeights)
            {
                if (roll < weight)
                {
                    symptom.Type = type;
                    break;
                }

                roll -= weight;
            }

            symptom.Intensity = Math.Clamp((int)Math.Round(45 + (random.NextDouble() - 0.5) * 70), 2, 80);
        }

        var shuffled = Enumerable.Range(0, symptoms.Count).OrderBy(_ => random.Next()).ToList();
        var cursor = 0;
        foreach (var (type, _) in SymptomWeights)
        {
            if (symptoms.All(symptom => symptom.Type != type))
            {
                symptoms[shuffled[cursor++]].Type = type;
            }
        }

        symptoms[shuffled[cursor++]].Intensity = 1;
        symptoms[shuffled[cursor++]].Intensity = 100;
        for (var high = 0; high < 12; high++)
        {
            symptoms[shuffled[cursor++]].Intensity = random.Next(81, 100);
        }
    }

    private static List<PlannedTieGroup> ApplyTieGroups<T>(List<T> drafts, string label, Guid patientId, string kind, int cuts)
        where T : TimedDraft
    {
        var ordered = drafts
            .Select((draft, index) => (Draft: draft, Id: DeriveId(patientId, kind, index)))
            .OrderBy(entry => entry, Comparer<(T Draft, Guid Id)>.Create(
                (first, second) => CompareServerOrder(first.Draft.ClientCreatedAt, first.Id, second.Draft.ClientCreatedAt, second.Id)))
            .Select(entry => entry.Draft)
            .ToList();

        var groups = new List<PlannedTieGroup>();
        for (var cut = 1; cut <= cuts; cut++)
        {
            // Posiciones 96 a 101 (base 1): el corte cae dentro del grupo aun con 4 registros nuevos encima.
            var firstPosition = (cut * PageSize) - (TieGroupSize - 2);
            var shared = ordered[firstPosition - 1].ClientCreatedAt;
            for (var offset = 0; offset < TieGroupSize; offset++)
            {
                ordered[firstPosition - 1 + offset].ClientCreatedAt = shared;
            }

            groups.Add(new PlannedTieGroup(label, shared, firstPosition, TieGroupSize));
        }

        return groups;
    }

    private static void MakeUnique<T>(List<T> drafts, DateTime now)
        where T : TimedDraft
    {
        var seen = new HashSet<DateTime>();
        foreach (var draft in drafts.OrderByDescending(draft => draft.ClientCreatedAt))
        {
            while (!seen.Add(draft.ClientCreatedAt) || draft.ClientCreatedAt > now)
            {
                draft.ClientCreatedAt = draft.ClientCreatedAt.AddSeconds(-1);
            }
        }
    }

    private abstract class TimedDraft
    {
        public DateTime ClientCreatedAt { get; set; }
    }

    private sealed class MealDraft : TimedDraft
    {
        public MealDraft(Slot slot, DateTime consumedAt, DateTime clientCreatedAt)
        {
            Slot = slot;
            ConsumedAt = consumedAt;
            ClientCreatedAt = clientCreatedAt;
        }

        public Slot Slot { get; }

        public DateTime ConsumedAt { get; }

        public MealTime MealTime { get; set; }

        public IReadOnlyList<PlannedMealItem> Items { get; set; } = [];
    }

    private sealed class SymptomDraft : TimedDraft
    {
        public SymptomDraft(DateTime occurredAt, DateTime clientCreatedAt)
        {
            OccurredAt = occurredAt;
            ClientCreatedAt = clientCreatedAt;
        }

        public DateTime OccurredAt { get; }

        public SymptomType Type { get; set; }

        public int Intensity { get; set; }
    }

    private sealed class FoodPicker
    {
        private readonly IReadOnlyList<CatalogFood> _catalog;
        private readonly Random _random;

        public FoodPicker(IReadOnlyList<CatalogFood> catalog, Random random)
        {
            _catalog = catalog;
            _random = random;
        }

        public void Require(string name)
        {
            if (_catalog.All(food => food.Name != name))
            {
                throw new InvalidOperationException($"Falta en el catálogo el alimento «{name}».");
            }
        }

        public string Pick(FodmapLevel level, IReadOnlySet<string> exclude)
        {
            var candidates = _catalog.Where(food => food.Level == level && !exclude.Contains(food.Name)).ToList();
            if (candidates.Count == 0)
            {
                throw new InvalidOperationException($"El catálogo no tiene alimentos de nivel {level} disponibles.");
            }

            var peruvian = candidates.Where(food => food.IsPeruvian).ToList();
            var others = candidates.Where(food => !food.IsPeruvian).ToList();
            var pool = peruvian.Count > 0 && (others.Count == 0 || _random.NextDouble() < PeruvianPreference) ? peruvian : others;
            return pool[_random.Next(pool.Count)].Name;
        }
    }
}
