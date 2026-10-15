using Cauce.Application.Common.Exceptions;
using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry.Enums;
using Cauce.Domain.Identity;
using Cauce.Domain.Patients.Enums;
using Cauce.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cauce.Infrastructure.Persistence.Seeders;

/// <summary>
/// Seeder de desarrollo del paciente de volumen: una cuenta lista para el Diario con 450 comidas y 180
/// síntomas sintéticos, para probar la app en un celular. Solo corre en Development y con
/// <c>VolumePatient:Enabled</c>; sin <c>VolumePatient__Regenerate</c>, una segunda ejecución no cambia nada.
/// </summary>
public sealed class VolumePatientSeeder
{
    private const string ProbeFoodCategory = "cereales";

    private static readonly DevelopmentPatientProfile Profile = new(
        new DateOnly(1988, 3, 22), BiologicalSex.Male, 74m, 170m, IbsSubtype.IbsM, new DateOnly(2023, 6, 1),
        PainSeverity: 70, PainFrequency: 50, BloatingSeverity: 60, BowelHabitsDissatisfaction: 55, LifeInterference: 45);

    private readonly CauceDbContext _context;
    private readonly DevelopmentPatientAccount _account;
    private readonly IOptions<VolumePatientOptions> _options;
    private readonly VolumePatientSwitches _switches;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<VolumePatientSeeder> _logger;

    /// <summary>
    /// Inicializa el seeder con sus dependencias.
    /// </summary>
    public VolumePatientSeeder(
        CauceDbContext context,
        DevelopmentPatientAccount account,
        IOptions<VolumePatientOptions> options,
        VolumePatientSwitches switches,
        TimeProvider timeProvider,
        ILogger<VolumePatientSeeder> logger)
    {
        _context = context;
        _account = account;
        _options = options;
        _switches = switches;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>
    /// Siembra el paciente si no existe, lo regenera si se pidió y retira el alimento de prueba si se pidió,
    /// en ese orden.
    /// </summary>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Tarea que representa la operación asíncrona.</returns>
    public async Task SeedAsync(CancellationToken ct = default)
    {
        var options = _options.Value;
        if (!options.Enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(options.Email) || string.IsNullOrWhiteSpace(options.Password))
        {
            _logger.LogWarning(
                "Volume patient is enabled but its email or the VolumePatient:Password user-secret is missing; nothing was seeded.");
            return;
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var email = EmailNormalization.Normalize(options.Email);
        var patientId = await _context.Users
            .Where(user => user.Email == email)
            .Select(user => (Guid?)user.Id)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (!VolumeHistoryGenerator.CanSeedAt(now))
        {
            _logger.LogWarning(
                "Volume patient seeding skipped: it is before 00:30 in Lima and a 'today' row could fall on yesterday. Restart after 00:30.");
        }
        else if (patientId is null)
        {
            await SeedNewPatientAsync(email, options, now, ct).ConfigureAwait(false);
        }
        else if (_switches.Regenerate)
        {
            await RegenerateAsync(patientId.Value, now, ct).ConfigureAwait(false);
        }
        else
        {
            await WarnIfStaleAsync(patientId.Value, now, ct).ConfigureAwait(false);
        }

        if (_switches.ProbeFoodRetired)
        {
            await RetireProbeFoodAsync(now, ct).ConfigureAwait(false);
        }
    }

    private async Task SeedNewPatientAsync(string email, VolumePatientOptions options, DateTime now, CancellationToken ct)
    {
        var catalog = await LoadCatalogAsync(ct).ConfigureAwait(false);

        User user;
        try
        {
            user = await _account
                .ProvisionAsync(email, options.FullName, options.Password, Profile, now.AddDays(-95), now.AddDays(-90), ct)
                .ConfigureAwait(false);
        }
        catch (KeycloakIntegrationException)
        {
            _logger.LogWarning(
                "Volume patient skipped: Keycloak rejected the account or its password (check the realm password policy). Nothing was seeded locally.");
            return;
        }

        var plan = TryGenerate(now, user.Id, catalog.Foods);
        if (plan is null)
        {
            return;
        }

        await EnsureProbeFoodAsync(now, ct).ConfigureAwait(false);
        AddPlan(plan, user.Id, catalog.IdsByName, now);
        await _context.SaveChangesAsync(ct).ConfigureAwait(false);

        _logger.LogInformation("Seeded volume patient {UserId}.", user.Id);
        _logger.LogInformation("{Manifest}", VolumeHistoryManifest.Build(plan));
    }

    private async Task RegenerateAsync(Guid patientId, DateTime now, CancellationToken ct)
    {
        var catalog = await LoadCatalogAsync(ct).ConfigureAwait(false);
        var plan = TryGenerate(now, patientId, catalog.Foods);
        if (plan is null)
        {
            return;
        }

        // Síntomas antes que comidas: su FK a la comida pasaría a NULL y dejaría has_meal_association en true.
        await using var transaction = await _context.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        await _context.Symptoms.Where(symptom => symptom.PatientId == patientId).ExecuteDeleteAsync(ct).ConfigureAwait(false);
        await _context.Meals.Where(meal => meal.PatientId == patientId).ExecuteDeleteAsync(ct).ConfigureAwait(false);
        await _context.CustomFoods.Where(food => food.PatientId == patientId).ExecuteDeleteAsync(ct).ConfigureAwait(false);

        await EnsureProbeFoodAsync(now, ct).ConfigureAwait(false);
        AddPlan(plan, patientId, catalog.IdsByName, now);
        await _context.SaveChangesAsync(ct).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);

        _logger.LogInformation("Regenerated volume patient {UserId}.", patientId);
        _logger.LogInformation("{Manifest}", VolumeHistoryManifest.Build(plan));
    }

    private async Task WarnIfStaleAsync(Guid patientId, DateTime now, CancellationToken ct)
    {
        var latestMeal = await _context.Meals
            .Where(meal => meal.PatientId == patientId)
            .MaxAsync(meal => (DateTime?)meal.ClientCreatedAt, ct)
            .ConfigureAwait(false);
        var latestSymptom = await _context.Symptoms
            .Where(symptom => symptom.PatientId == patientId)
            .MaxAsync(symptom => (DateTime?)symptom.ClientCreatedAt, ct)
            .ConfigureAwait(false);
        var latest = latestMeal > latestSymptom || latestSymptom is null ? latestMeal : latestSymptom;

        if (latest is null || now - latest.Value > TimeSpan.FromDays(1))
        {
            _logger.LogWarning(
                "Volume patient history is more than one day old (latest row {LatestUtc:O}); the manifest no longer matches the screen. Restart with {Variable}=true on the day of the test pass.",
                latest,
                VolumePatientSwitches.RegenerateVariable);
            return;
        }

        _logger.LogInformation("Volume patient already present; skipping seed (idempotent no-op).");
    }

    private async Task EnsureProbeFoodAsync(DateTime now, CancellationToken ct)
    {
        var probe = await _context.FoodItems
            .FirstOrDefaultAsync(food => food.Name == VolumeHistoryGenerator.ProbeFoodName, ct)
            .ConfigureAwait(false);
        if (probe is null)
        {
            _context.FoodItems.Add(FoodItem.SeedEntry(
                Guid.NewGuid(), VolumeHistoryGenerator.ProbeFoodName, ProbeFoodCategory,
                100m, 3m, 20m, 1m, 1m, FodmapLevel.Low, null, false, now));
            return;
        }

        probe.Reactivate(now);
    }

    private async Task RetireProbeFoodAsync(DateTime now, CancellationToken ct)
    {
        var probe = await _context.FoodItems
            .FirstOrDefaultAsync(food => food.Name == VolumeHistoryGenerator.ProbeFoodName, ct)
            .ConfigureAwait(false);
        if (probe is null)
        {
            _logger.LogWarning("Probe food retirement requested, but the probe food does not exist; seed the volume patient first.");
            return;
        }

        probe.Deactivate(now);
        await _context.SaveChangesAsync(ct).ConfigureAwait(false);
        _logger.LogInformation("Probe food {FoodItemId} retired: registering it now returns food_item_not_found.", probe.Id);
    }

    private async Task<(IReadOnlyList<CatalogFood> Foods, IReadOnlyDictionary<string, Guid> IdsByName)> LoadCatalogAsync(CancellationToken ct)
    {
        var foods = await _context.FoodItems
            .AsNoTracking()
            .Where(food => food.IsActive && food.Name != VolumeHistoryGenerator.ProbeFoodName)
            .Select(food => new { food.Id, food.Name, food.FodmapLevel, food.IsPeruvian })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return (
            foods.Select(food => new CatalogFood(food.Name, food.FodmapLevel, food.IsPeruvian)).ToList(),
            foods.ToDictionary(food => food.Name, food => food.Id));
    }

    private VolumeHistoryPlan? TryGenerate(DateTime now, Guid patientId, IReadOnlyList<CatalogFood> catalog)
    {
        try
        {
            return VolumeHistoryGenerator.Generate(now, patientId, catalog);
        }
        catch (InvalidOperationException exception)
        {
            _logger.LogWarning(exception, "Volume patient history could not be planned; nothing was seeded.");
            return null;
        }
    }

    private void AddPlan(VolumeHistoryPlan plan, Guid patientId, IReadOnlyDictionary<string, Guid> foodIds, DateTime now)
    {
        var customFoodIds = new List<Guid>();
        foreach (var planned in plan.CustomFoods)
        {
            var customFood = CustomFood.Create(planned.Id, patientId, planned.Name, planned.PortionSizeGrams, now.AddDays(-89));
            foreach (var ingredient in planned.Ingredients)
            {
                customFood.AddIngredient(foodIds[ingredient.FoodName], ingredient.Grams);
            }

            _context.CustomFoods.Add(customFood);
            customFoodIds.Add(customFood.Id);
        }

        foreach (var planned in plan.Meals)
        {
            var items = planned.Items.Select(item => item.FoodName is not null
                ? new MealItemInput(foodIds[item.FoodName], null, item.Quantity, item.Unit)
                : new MealItemInput(null, customFoodIds[item.CustomFoodIndex!.Value], item.Quantity, item.Unit));
            _context.Meals.Add(Meal.Register(
                planned.Id, planned.ClientGuid, patientId, planned.MealTime, planned.ConsumedAt, planned.ClientCreatedAt, items, now));
        }

        foreach (var planned in plan.Symptoms)
        {
            var symptom = Symptom.Report(
                planned.Id, planned.ClientGuid, patientId, planned.Type, planned.Intensity, planned.OccurredAt, planned.ClientCreatedAt, now);
            if (planned.AssociatedMealId is { } mealId)
            {
                symptom.AssociateWithMeal(mealId, now);
            }

            _context.Symptoms.Add(symptom);
        }
    }
}
