using System.Net;
using Cauce.Api.IntegrationTests.Identity.Support;
using Cauce.Domain.Auditing.Enums;
using Cauce.Domain.Recommendations;
using Cauce.Domain.Recommendations.Enums;
using Cauce.Domain.Recommendations.ValueObjects;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Api.IntegrationTests.Recommendations;

/// <summary>
/// Pruebas de la auditoría de los intentos de decidir sobre una recomendación cerrada: el servidor
/// responde 409 y deja registro del nutricionista, la recomendación y la acción, sin el texto de la nota.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ClosedRecommendationAttemptAuditTests
    : IntegrationTestBase, IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>
{
    private const string AttemptNote = "Texto de prueba que no debe quedar en la auditoría";

    /// <summary>
    /// Inicializa la prueba con los fixtures compartidos.
    /// </summary>
    public ClosedRecommendationAttemptAuditTests(PostgresFixture postgres, RedisFixture redis)
        : base(postgres, redis)
    {
    }

    public enum Closed
    {
        ApprovedAsGenerated,
        ApprovedWithChanges,
        Manual,
        Rejected
    }

    private async Task<Guid> SeedClosedAsync(Guid patientId, Guid nutritionistId, Closed state)
    {
        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var now = DateTime.UtcNow.AddHours(-1);
        Recommendation recommendation;
        if (state == Closed.Manual)
        {
            recommendation = Recommendation.CreateManual(
                patientId, nutritionistId, "Hidratación entre comidas", "Beber agua entre comidas.", null,
                "Indicada por estreñimiento leve.", null, now);
        }
        else
        {
            var modelVersionId = await db.ModelVersions.AsNoTracking().Select(v => v.Id).FirstAsync();
            var foodId = await db.FoodItems.AsNoTracking().Select(f => f.Id).FirstAsync();
            recommendation = Recommendation.Generate(
                patientId, modelVersionId, ConfidenceScore.Create(0.80m), ExplanationSource.LlmGenerated, "Explicación.",
                [RecommendationItem.Create(foodId, ActionType.Reduce, null, null)], now, TimeSpan.FromHours(72));
            recommendation.MarkPendingReview(now);
            switch (state)
            {
                case Closed.ApprovedAsGenerated:
                    recommendation.Approve(nutritionistId, "Nota clínica original.", now);
                    break;
                case Closed.ApprovedWithChanges:
                    recommendation.ModifyByNutritionist(nutritionistId, "Nota clínica original.", now);
                    break;
                default:
                    recommendation.Reject(nutritionistId, "Motivo de rechazo original.", now);
                    break;
            }
        }

        db.Recommendations.Add(recommendation);
        await db.SaveChangesAsync();
        return recommendation.Id;
    }

    private async Task<(Guid Patient, (Guid Id, string KeycloakId, string Email) Nutritionist)> SeedPairAsync()
    {
        var patient = await SeedPatientAsync();
        await SeedPatientProfileAsync(patient.Id);
        var nutritionist = await SeedNutritionistAsync();
        await AssignAsync(nutritionist.Id, patient.Id);
        return (patient.Id, nutritionist);
    }

    private static Task<HttpResponseMessage> AttemptAsync(HttpClient client, Guid id, string action) => action switch
    {
        "approve" => PostWithKey(client, $"/api/v1/recommendations/{id}/approve", new { note = AttemptNote }),
        "reject" => PostWithKey(client, $"/api/v1/recommendations/{id}/reject", new { reason = AttemptNote }),
        "modify" => PostWithKey(client, $"/api/v1/recommendations/{id}/modify", new { clinicalNote = AttemptNote, title = "Título de prueba" }),
        _ => PostWithKey(client, $"/api/v1/recommendations/{id}/archive", new { reason = "ManualSubstitution" })
    };

    [SkippableTheory]
    [InlineData(Closed.ApprovedAsGenerated, "modify")]
    [InlineData(Closed.ApprovedWithChanges, "modify")]
    [InlineData(Closed.Manual, "modify")]
    [InlineData(Closed.Rejected, "modify")]
    [InlineData(Closed.Rejected, "approve")]
    [InlineData(Closed.Rejected, "reject")]
    [InlineData(Closed.Rejected, "archive")]
    public async Task Attempt_OnAClosedRecommendation_Returns409AndIsAudited(Closed state, string action)
    {
        SkipIfUnavailable();
        var (patientId, nutritionist) = await SeedPairAsync();
        var recommendationId = await SeedClosedAsync(patientId, nutritionist.Id, state);
        var client = NutritionistClient(nutritionist.KeycloakId, nutritionist.Email);

        var response = await AttemptAsync(client, recommendationId, action);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var audit = (await AuditLogsAsync(nameof(Recommendation), recommendationId, AuditActionType.BlockedChangeAttempt))
            .Should().ContainSingle().Subject;
        audit.ActorUserId.Should().Be(nutritionist.Id);
        audit.OccurredAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        ContextValue(audit, "attempted_action").Should().Be(action);
        ContextValue(audit, "status").Should().NotBeNullOrEmpty();
        audit.AdditionalContext.Should().NotContain(AttemptNote);
        audit.NewValuesHash.Should().BeNull();
    }

    [SkippableFact]
    public async Task Attempt_OnAClosedRecommendation_LeavesItUnchanged()
    {
        SkipIfUnavailable();
        var (patientId, nutritionist) = await SeedPairAsync();
        var recommendationId = await SeedClosedAsync(patientId, nutritionist.Id, Closed.ApprovedAsGenerated);

        await AttemptAsync(NutritionistClient(nutritionist.KeycloakId, nutritionist.Email), recommendationId, "modify");

        var (scope, db) = CreateDbScope();
        using var _ = scope;
        var stored = await db.Recommendations.AsNoTracking().SingleAsync(r => r.Id == recommendationId);
        stored.Status.Should().Be(RecommendationStatus.Approved);
        stored.NutritionistNote.Should().Be("Nota clínica original.");
        stored.Title.Should().BeNull();
    }

    [SkippableFact]
    public async Task Approve_OnAPendingRecommendation_IsNotAuditedAsABlockedAttempt()
    {
        SkipIfUnavailable();
        var (patientId, nutritionist) = await SeedPairAsync();
        var (scope, db) = CreateDbScope();
        Guid id;
        using (scope)
        {
            var modelVersionId = await db.ModelVersions.AsNoTracking().Select(v => v.Id).FirstAsync();
            var foodId = await db.FoodItems.AsNoTracking().Select(f => f.Id).FirstAsync();
            var pending = Recommendation.Generate(
                patientId, modelVersionId, ConfidenceScore.Create(0.80m), ExplanationSource.LlmGenerated, "Explicación.",
                [RecommendationItem.Create(foodId, ActionType.Reduce, null, null)], DateTime.UtcNow, TimeSpan.FromHours(72));
            pending.MarkPendingReview(DateTime.UtcNow);
            db.Recommendations.Add(pending);
            await db.SaveChangesAsync();
            id = pending.Id;
        }

        var response = await AttemptAsync(NutritionistClient(nutritionist.KeycloakId, nutritionist.Email), id, "approve");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await AuditLogsAsync(nameof(Recommendation), id, AuditActionType.BlockedChangeAttempt)).Should().BeEmpty();
    }
}
