using System.Net;
using System.Text.Json;
using Cauce.Api.IntegrationTests.Identity.Support;
using Cauce.Domain.Recommendations;
using Cauce.Domain.Recommendations.Enums;
using Cauce.Domain.Recommendations.ValueObjects;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Api.IntegrationTests.Recommendations;

/// <summary>
/// Pruebas de la aprobación con modificación de una recomendación en revisión cuando el nutricionista cambia
/// sus ítems o su contenido, contra la base real.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ModifyRecommendationItemsApiTests
    : IntegrationTestBase, IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>
{
    private const string ClinicalNote = "Ajuste de alimentos tras revisar el diario del paciente.";

    /// <summary>
    /// Inicializa la prueba con los fixtures compartidos.
    /// </summary>
    public ModifyRecommendationItemsApiTests(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis)
    {
    }

    [SkippableFact]
    public async Task Modify_AddingAFood_Returns204AndStoresTheNewItem()
    {
        SkipIfUnavailable();
        var seed = await SeedPendingReviewAsync();

        var response = await ModifyAsync(seed, new
        {
            clinicalNote = ClinicalNote,
            items = new[]
            {
                Item(seed.Foods[0], ActionType.Reduce),
                Item(seed.Foods[1], ActionType.Avoid),
                Item(seed.Foods[2], ActionType.Suggest)
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
        var stored = await LoadAsync(seed.RecommendationId);
        stored.Status.Should().Be(RecommendationStatus.ModifiedApproved);
        stored.NutritionistNote.Should().Be(ClinicalNote);
        Summarize(stored).Should().BeEquivalentTo(new[]
        {
            (seed.Foods[0], ActionType.Reduce, (Guid?)null),
            (seed.Foods[1], ActionType.Avoid, (Guid?)null),
            (seed.Foods[2], ActionType.Suggest, (Guid?)null)
        });
    }

    [SkippableFact]
    public async Task Modify_RemovingAFood_Returns204AndDropsTheItem()
    {
        SkipIfUnavailable();
        var seed = await SeedPendingReviewAsync();

        var response = await ModifyAsync(seed, new
        {
            clinicalNote = ClinicalNote,
            items = new[] { Item(seed.Foods[0], ActionType.Reduce) }
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
        var stored = await LoadAsync(seed.RecommendationId);
        stored.Status.Should().Be(RecommendationStatus.ModifiedApproved);
        Summarize(stored).Should().BeEquivalentTo(new[] { (seed.Foods[0], ActionType.Reduce, (Guid?)null) });
    }

    [SkippableFact]
    public async Task Modify_SubstitutingAFood_Returns204AndStoresTheSubstitute()
    {
        SkipIfUnavailable();
        var seed = await SeedPendingReviewAsync();

        var response = await ModifyAsync(seed, new
        {
            clinicalNote = ClinicalNote,
            items = new[]
            {
                Item(seed.Foods[0], ActionType.Reduce),
                Item(seed.Foods[1], ActionType.Substitute, seed.Foods[3])
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
        var stored = await LoadAsync(seed.RecommendationId);
        stored.Status.Should().Be(RecommendationStatus.ModifiedApproved);
        Summarize(stored).Should().BeEquivalentTo(new[]
        {
            (seed.Foods[0], ActionType.Reduce, (Guid?)null),
            (seed.Foods[1], ActionType.Substitute, (Guid?)seed.Foods[3])
        });
    }

    [SkippableFact]
    public async Task Modify_ReplacingTheWholeList_Returns204AndKeepsOnlyTheNewItems()
    {
        SkipIfUnavailable();
        var seed = await SeedPendingReviewAsync();
        var before = await LoadAsync(seed.RecommendationId);

        var response = await ModifyAsync(seed, new
        {
            clinicalNote = ClinicalNote,
            items = new[]
            {
                Item(seed.Foods[2], ActionType.Avoid),
                Item(seed.Foods[3], ActionType.Suggest)
            }
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
        var stored = await LoadAsync(seed.RecommendationId);
        stored.Status.Should().Be(RecommendationStatus.ModifiedApproved);
        Summarize(stored).Should().BeEquivalentTo(new[]
        {
            (seed.Foods[2], ActionType.Avoid, (Guid?)null),
            (seed.Foods[3], ActionType.Suggest, (Guid?)null)
        });
        stored.Items.Select(item => item.Id).Should().NotIntersectWith(before.Items.Select(item => item.Id));
    }

    [SkippableTheory]
    [InlineData("title")]
    [InlineData("description")]
    [InlineData("steps")]
    public async Task Modify_WithoutItems_ChangesOnlyTheSentFieldAndKeepsTheItems(string field)
    {
        SkipIfUnavailable();
        var seed = await SeedPendingReviewAsync();
        var before = await LoadAsync(seed.RecommendationId);
        string[] newSteps = ["Primer paso ajustado", "Segundo paso ajustado"];
        object body = field switch
        {
            "title" => new { clinicalNote = ClinicalNote, title = "Título ajustado" },
            "description" => new { clinicalNote = ClinicalNote, description = "Descripción ajustada." },
            _ => new { clinicalNote = ClinicalNote, steps = newSteps }
        };

        var response = await ModifyAsync(seed, body);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
        var stored = await LoadAsync(seed.RecommendationId);
        stored.Status.Should().Be(RecommendationStatus.ModifiedApproved);
        stored.Title.Should().Be(field == "title" ? "Título ajustado" : before.Title);
        stored.Description.Should().Be(field == "description" ? "Descripción ajustada." : before.Description);
        stored.Steps.Should().Equal(field == "steps" ? newSteps : before.Steps);
        stored.Items.Select(item => item.Id).Should().BeEquivalentTo(before.Items.Select(item => item.Id));
        Summarize(stored).Should().BeEquivalentTo(Summarize(before));
    }

    [SkippableTheory]
    [InlineData("foodId")]
    [InlineData("substituteFoodId")]
    public async Task Modify_WithAFoodOutsideTheCatalog_Returns400AndLeavesTheRecommendationPending(string field)
    {
        SkipIfUnavailable();
        var seed = await SeedPendingReviewAsync();
        var before = await LoadAsync(seed.RecommendationId);
        var unknownFood = Guid.NewGuid();
        var items = field == "foodId"
            ? new[] { Item(seed.Foods[0], ActionType.Reduce), Item(unknownFood, ActionType.Avoid) }
            : new[] { Item(seed.Foods[0], ActionType.Reduce), Item(seed.Foods[1], ActionType.Substitute, unknownFood) };

        var response = await ModifyAsync(seed, new { clinicalNote = ClinicalNote, items });

        var payload = await response.Content.ReadAsStringAsync();
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, payload);
        using var problem = JsonDocument.Parse(payload);
        problem.RootElement.GetProperty("errorCode").GetString().Should().Be("validation_error");
        problem.RootElement.GetProperty("errors").TryGetProperty($"items[1].{field}", out var messages).Should().BeTrue(payload);
        messages.EnumerateArray().Should().ContainSingle();
        var stored = await LoadAsync(seed.RecommendationId);
        stored.Status.Should().Be(RecommendationStatus.PendingReview);
        Summarize(stored).Should().BeEquivalentTo(Summarize(before));
    }

    private sealed record PendingSeed(Guid RecommendationId, string NutritionistKeycloakId, string NutritionistEmail, IReadOnlyList<Guid> Foods);

    // Recomendación en revisión con dos ítems (reducir y evitar) y un paso; los demás alimentos quedan libres.
    private async Task<PendingSeed> SeedPendingReviewAsync()
    {
        var patient = await SeedPatientAsync();
        await SeedPatientProfileAsync(patient.Id);
        var nutritionist = await SeedNutritionistAsync();
        await AssignAsync(nutritionist.Id, patient.Id);

        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var modelVersionId = await db.ModelVersions.AsNoTracking().Select(v => v.Id).FirstAsync();
        var foods = await db.FoodItems.AsNoTracking().OrderBy(f => f.Name).Select(f => f.Id).Take(5).ToListAsync();
        var now = DateTime.UtcNow;
        var recommendation = Recommendation.Generate(
            patient.Id, modelVersionId, ConfidenceScore.Create(0.80m), ExplanationSource.LlmGenerated, "Explicación.",
            [
                RecommendationItem.Create(foods[0], ActionType.Reduce, "Motivo original.", null),
                RecommendationItem.Create(foods[1], ActionType.Avoid, null, null)
            ],
            now, TimeSpan.FromHours(72), ["Paso original"]);
        recommendation.MarkPendingReview(now);
        db.Recommendations.Add(recommendation);
        await db.SaveChangesAsync();

        return new PendingSeed(recommendation.Id, nutritionist.KeycloakId, nutritionist.Email, foods);
    }

    private Task<HttpResponseMessage> ModifyAsync(PendingSeed seed, object body) =>
        PostWithKey(
            NutritionistClient(seed.NutritionistKeycloakId, seed.NutritionistEmail),
            $"/api/v1/recommendations/{seed.RecommendationId}/modify",
            body);

    private static object Item(Guid foodId, ActionType actionType, Guid? substituteFoodId = null) =>
        new { foodId, actionType = actionType.ToString(), reasoning = (string?)null, substituteFoodId };

    private async Task<Recommendation> LoadAsync(Guid recommendationId)
    {
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        return await db.Recommendations.AsNoTracking().Include(r => r.Items).SingleAsync(r => r.Id == recommendationId);
    }

    private static IEnumerable<(Guid FoodId, ActionType ActionType, Guid? SubstituteFoodId)> Summarize(Recommendation recommendation) =>
        recommendation.Items.Select(item => (item.FoodId, item.ActionType, item.SubstituteFoodId));
}
