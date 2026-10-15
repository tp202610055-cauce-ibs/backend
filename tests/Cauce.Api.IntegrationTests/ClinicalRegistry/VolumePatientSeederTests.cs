using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cauce.Api.IntegrationTests.Identity.Support;
using Cauce.Application.Common.Exceptions;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Domain.ClinicalRegistry.Enums;
using Cauce.Domain.Patients.Enums;
using Cauce.Infrastructure.Identity;
using Cauce.Infrastructure.Persistence.Seeders;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Cauce.Api.IntegrationTests.ClinicalRegistry;

/// <summary>
/// Pruebas de integración del paciente de volumen: siembra, idempotencia, regeneración, paginación por la
/// API y retiro del alimento de prueba. El reloj se fija al mediodía de Lima de una fecha pasada.
/// </summary>
[Trait("Category", "Integration")]
public sealed class VolumePatientSeederTests
    : IntegrationTestBase, IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>
{
    private const string VolumeEmail = "paciente.volumen@cauce.local";
    private static readonly DateTime Noon = new(2026, 9, 15, 17, 0, 0, DateTimeKind.Utc);

    /// <summary>
    /// Inicializa la prueba con los fixtures compartidos.
    /// </summary>
    public VolumePatientSeederTests(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis)
    {
    }

    private async Task RunSeederAsync(DateTime utcNow, bool regenerate = false, bool probeFoodRetired = false, string password = "Volumen#Prueba2026")
    {
        var options = Options.Create(new VolumePatientOptions
        {
            Enabled = true,
            Email = VolumeEmail,
            FullName = "Paciente Volumen",
            Password = password
        });

        using var scope = Factory.Services.CreateScope();
        var seeder = ActivatorUtilities.CreateInstance<VolumePatientSeeder>(
            scope.ServiceProvider,
            options,
            new VolumePatientSwitches(regenerate, probeFoodRetired),
            new FakeTimeProvider(new DateTimeOffset(utcNow)));
        await seeder.SeedAsync();
    }

    private async Task<Guid> PatientIdAsync()
    {
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        return await db.Users.Where(user => user.Email == VolumeEmail).Select(user => user.Id).SingleAsync();
    }

    [SkippableFact]
    public async Task SeedAsync_CreatesAReadyPatientWithTheFullHistory()
    {
        SkipIfUnavailable();

        await RunSeederAsync(Noon);

        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var user = await db.Users.AsNoTracking().SingleAsync(candidate => candidate.Email == VolumeEmail);
        user.EmailVerified.Should().BeTrue();
        user.IsInActivePilot.Should().BeTrue();

        var profile = await db.PatientProfiles.AsNoTracking().SingleAsync(candidate => candidate.UserId == user.Id);
        profile.OnboardingCompleted.Should().BeTrue();
        profile.IbsSubtype.Should().Be(IbsSubtype.IbsM);

        var consentService = Factory.Services.GetRequiredService<IConsentService>();
        var consent = await db.ConsentRecords.AsNoTracking().SingleAsync(candidate => candidate.UserId == user.Id);
        consent.ConsentTextHash.Should().Be(consentService.GetCurrentTextHash());
        (await db.IbsSssAssessments.CountAsync(candidate => candidate.PatientId == user.Id && candidate.AssessmentType == AssessmentType.Baseline))
            .Should().Be(1);
        (await db.NutritionistPatients.AnyAsync(link => link.PatientId == user.Id)).Should().BeFalse();

        (await db.Meals.CountAsync(meal => meal.PatientId == user.Id)).Should().Be(450);
        (await db.Symptoms.CountAsync(symptom => symptom.PatientId == user.Id)).Should().Be(180);
        (await db.CustomFoods.CountAsync(food => food.PatientId == user.Id)).Should().Be(3);

        var probe = await db.FoodItems.AsNoTracking().SingleAsync(food => food.Name == VolumeHistoryGenerator.ProbeFoodName);
        probe.IsActive.Should().BeTrue();
        (await db.FoodItems.CountAsync(food => food.IsActive)).Should().Be(FoodItemsSeeder.CatalogSize + 1);
        (await db.MealItems.AnyAsync(item => item.FoodId == probe.Id)).Should().BeFalse();

        (await AuditLogsAsync("Meal")).Should().HaveCount(450);
        (await AuditLogsAsync("Symptom")).Should().HaveCount(180);
    }

    [SkippableFact]
    public async Task SeedAsync_RunTwice_ChangesNothing()
    {
        SkipIfUnavailable();
        await RunSeederAsync(Noon);
        var before = await SnapshotAsync();

        await RunSeederAsync(Noon.AddHours(3));

        (await SnapshotAsync()).Should().BeEquivalentTo(before, options => options.WithStrictOrdering());
    }

    [SkippableFact]
    public async Task SeedAsync_Regenerate_RebuildsTheHistoryWithoutOrphans()
    {
        SkipIfUnavailable();
        await RunSeederAsync(Noon);
        var later = Noon.AddDays(2);

        await RunSeederAsync(later, regenerate: true);

        var patientId = await PatientIdAsync();
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        (await db.Meals.CountAsync(meal => meal.PatientId == patientId)).Should().Be(450);
        (await db.Symptoms.CountAsync(symptom => symptom.PatientId == patientId)).Should().Be(180);
        (await db.CustomFoods.CountAsync(food => food.PatientId == patientId)).Should().Be(3);
        (await db.Meals.Where(meal => meal.PatientId == patientId).MaxAsync(meal => meal.ClientCreatedAt))
            .Should().BeCloseTo(later, TimeSpan.FromMinutes(15));

        var mealIds = await db.Meals.Where(meal => meal.PatientId == patientId).Select(meal => meal.Id).ToListAsync();
        var symptoms = await db.Symptoms.AsNoTracking().Where(symptom => symptom.PatientId == patientId).ToListAsync();
        symptoms.Should().OnlyContain(symptom => symptom.HasMealAssociation == (symptom.AssociatedMealId != null));
        symptoms.Where(symptom => symptom.AssociatedMealId != null)
            .Should().OnlyContain(symptom => mealIds.Contains(symptom.AssociatedMealId!.Value));

        (await db.Users.CountAsync(user => user.Id == patientId)).Should().Be(1);
        (await db.ConsentRecords.CountAsync(record => record.UserId == patientId)).Should().Be(1);
        (await db.PatientProfiles.CountAsync(profile => profile.UserId == patientId)).Should().Be(1);
        (await db.IbsSssAssessments.CountAsync(assessment => assessment.PatientId == patientId)).Should().Be(1);

        // 630 filas de la siembra y 1260 de la regeneración (borrado e inserción).
        ((await AuditLogsAsync("Meal")).Count + (await AuditLogsAsync("Symptom")).Count).Should().Be(630 + 1260);
    }

    [SkippableFact]
    public async Task SeedAsync_BeforeHalfPastMidnightInLima_SkipsEvenWithRegenerate()
    {
        SkipIfUnavailable();
        await RunSeederAsync(Noon);
        var before = await SnapshotAsync();
        var justBefore = VolumeHistoryGenerator.FromLima(VolumeHistoryGenerator.ToLima(Noon).Date.AddDays(1).AddMinutes(29));

        await RunSeederAsync(justBefore, regenerate: true);

        (await SnapshotAsync()).Should().BeEquivalentTo(before, options => options.WithStrictOrdering());
    }

    [SkippableFact]
    public async Task HistoryEndpoints_PageThroughEveryRowWithTiesOverEachCut()
    {
        SkipIfUnavailable();
        await RunSeederAsync(Noon);
        var keycloakId = await KeycloakIdAsync();
        var client = PatientClient(keycloakId, VolumeEmail);

        var meals = await PageAsync(client, "meals", "mealId", expectedPages: 5);
        var symptoms = await PageAsync(client, "symptoms", "symptomId", expectedPages: 2);

        meals.SelectMany(page => page).Should().HaveCount(450).And.OnlyHaveUniqueItems();
        symptoms.SelectMany(page => page).Should().HaveCount(180).And.OnlyHaveUniqueItems();

        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var mealCreatedAt = await db.Meals.ToDictionaryAsync(meal => meal.Id, meal => meal.ClientCreatedAt);
        var symptomCreatedAt = await db.Symptoms.ToDictionaryAsync(symptom => symptom.Id, symptom => symptom.ClientCreatedAt);
        for (var page = 0; page < meals.Count - 1; page++)
        {
            mealCreatedAt[meals[page][^1]].Should().Be(mealCreatedAt[meals[page + 1][0]], $"el corte {(page + 1) * 100} cae dentro de un grupo");
        }

        symptomCreatedAt[symptoms[0][^1]].Should().Be(symptomCreatedAt[symptoms[1][0]]);
    }

    [SkippableFact]
    public async Task SeedAsync_ProbeFoodRetired_MakesRegisteringItFailWithFoodItemNotFound()
    {
        SkipIfUnavailable();
        await RunSeederAsync(Noon);
        Guid probeId;
        var (scope, db) = CreateDbScope();
        using (scope)
        {
            probeId = await db.FoodItems.Where(food => food.Name == VolumeHistoryGenerator.ProbeFoodName).Select(food => food.Id).SingleAsync();
        }

        await RunSeederAsync(Noon.AddHours(1), probeFoodRetired: true);

        var client = PatientClient(await KeycloakIdAsync(), VolumeEmail);
        var at = DateTime.UtcNow.AddMinutes(-5);
        var response = await PostWithKey(client, "/api/v1/meals", new
        {
            mealTime = "Snack",
            consumedAt = at,
            clientCreatedAt = at,
            items = new[] { new { foodId = probeId, quantity = 100m, unit = "Grams" } }
        });

        var raw = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.NotFound, raw);
        JsonDocument.Parse(raw).RootElement.GetProperty("errorCode").GetString().Should().Be("food_item_not_found");
    }

    [SkippableFact]
    public async Task ManifestSql_MatchesTheLimaDayCountsOfTheSeed()
    {
        SkipIfUnavailable();
        await RunSeederAsync(Noon);
        var patientId = await PatientIdAsync();

        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var meals = await db.Meals.Where(meal => meal.PatientId == patientId).Select(meal => meal.ConsumedAt).ToListAsync();
        var symptoms = await db.Symptoms.Where(symptom => symptom.PatientId == patientId).Select(symptom => symptom.OccurredAt).ToListAsync();
        var expected = Enumerable.Range(0, 89)
            .Select(offset => DateOnly.FromDateTime(VolumeHistoryGenerator.ToLima(Noon)).AddDays(offset - 88))
            .Select(day => (Day: day,
                Meals: meals.Count(at => DateOnly.FromDateTime(VolumeHistoryGenerator.ToLima(at)) == day),
                Symptoms: symptoms.Count(at => DateOnly.FromDateTime(VolumeHistoryGenerator.ToLima(at)) == day)))
            .ToList();

        // La misma consulta de SEED-USERS.md, con el instante fijado en lugar de now().
        var sql = ManifestSql.Replace("now()", $"'{Noon:O}'::timestamptz", StringComparison.Ordinal);
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var actual = new List<(DateOnly Day, int Meals, int Symptoms)>();
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                actual.Add((DateOnly.FromDateTime(reader.GetDateTime(0)), (int)reader.GetInt64(1), (int)reader.GetInt64(2)));
            }
        }

        actual.Should().Equal(expected);
        actual.Sum(row => row.Meals).Should().Be(450);
        actual.Sum(row => row.Symptoms).Should().Be(180);
    }

    private const string ManifestSql = """
        WITH paciente AS (
            SELECT user_id FROM users WHERE email = 'paciente.volumen@cauce.local'
        ), hoy AS (
            SELECT (now() AT TIME ZONE 'UTC' - interval '5 hours')::date AS dia
        ), dias AS (
            SELECT generate_series((SELECT dia FROM hoy) - 88, (SELECT dia FROM hoy), interval '1 day')::date AS dia
        ), comidas AS (
            SELECT (consumed_at AT TIME ZONE 'UTC' - interval '5 hours')::date AS dia, count(*) AS n
            FROM meals WHERE patient_id = (SELECT user_id FROM paciente) GROUP BY 1
        ), sintomas AS (
            SELECT (occurred_at AT TIME ZONE 'UTC' - interval '5 hours')::date AS dia, count(*) AS n
            FROM symptoms WHERE patient_id = (SELECT user_id FROM paciente) GROUP BY 1
        )
        SELECT d.dia, coalesce(c.n, 0) AS comidas, coalesce(s.n, 0) AS sintomas
        FROM dias d
        LEFT JOIN comidas c USING (dia)
        LEFT JOIN sintomas s USING (dia)
        ORDER BY d.dia;
        """;

    [SkippableFact]
    public async Task SeedAsync_WithoutPassword_DoesNothing()
    {
        SkipIfUnavailable();

        await RunSeederAsync(Noon, password: string.Empty);

        var (scope, db) = CreateDbScope();
        using var _ = scope;
        (await db.Users.AnyAsync(user => user.Email == VolumeEmail)).Should().BeFalse();
    }

    [SkippableFact]
    public async Task SeedAsync_PasswordRejectedByKeycloak_DoesNothingLocally()
    {
        SkipIfUnavailable();
        Factory.KeycloakClient.ResetPasswordFailure = new KeycloakIntegrationException("Password policy rejected the password.", 400);
        try
        {
            await RunSeederAsync(Noon);
        }
        finally
        {
            Factory.KeycloakClient.ResetPasswordFailure = null;
        }

        var (scope, db) = CreateDbScope();
        using var _ = scope;
        (await db.Users.AnyAsync(user => user.Email == VolumeEmail)).Should().BeFalse();
    }

    private async Task<string> KeycloakIdAsync()
    {
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        return await db.Users.Where(user => user.Email == VolumeEmail).Select(user => user.KeycloakId).SingleAsync();
    }

    private static async Task<List<List<Guid>>> PageAsync(HttpClient client, string resource, string idProperty, int expectedPages)
    {
        var from = Uri.EscapeDataString(Noon.AddDays(-90).ToString("O"));
        var to = Uri.EscapeDataString(Noon.ToString("O"));
        var pages = new List<List<Guid>>();
        for (var page = 1; page <= expectedPages + 1; page++)
        {
            var response = await client.GetAsync($"/api/v1/{resource}?from={from}&to={to}&page={page}&pageSize=100");
            var raw = await response.Content.ReadAsStringAsync();
            response.StatusCode.Should().Be(HttpStatusCode.OK, raw);
            var items = JsonDocument.Parse(raw).RootElement.GetProperty("items").EnumerateArray()
                .Select(item => item.GetProperty(idProperty).GetGuid())
                .ToList();
            if (items.Count == 0)
            {
                break;
            }

            pages.Add(items);
        }

        pages.Should().HaveCount(expectedPages);
        return pages;
    }

    private async Task<object> SnapshotAsync()
    {
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var meals = await db.Meals.AsNoTracking().OrderBy(meal => meal.Id)
            .Select(meal => new { meal.Id, meal.ClientGuid, meal.ConsumedAt, meal.ClientCreatedAt }).ToListAsync();
        var symptoms = await db.Symptoms.AsNoTracking().OrderBy(symptom => symptom.Id)
            .Select(symptom => new { symptom.Id, symptom.ClientCreatedAt, symptom.AssociatedMealId, symptom.HasMealAssociation }).ToListAsync();
        var customFoods = await db.CustomFoods.AsNoTracking().OrderBy(food => food.Id).Select(food => new { food.Id, food.Name }).ToListAsync();
        var probe = await db.FoodItems.AsNoTracking().Where(food => food.Name == VolumeHistoryGenerator.ProbeFoodName)
            .Select(food => new { food.Id, food.IsActive }).SingleAsync();
        return new { meals, symptoms, customFoods, probe };
    }
}
