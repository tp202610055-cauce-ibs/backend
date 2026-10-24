using Cauce.Application.Recommendations.Contracts;
using Cauce.Domain.ClinicalRegistry.Enums;
using Cauce.Domain.Patients.Enums;
using Cauce.Domain.Recommendations;
using Cauce.Domain.Recommendations.Enums;
using Cauce.Domain.Recommendations.ValueObjects;
using Cauce.Infrastructure.Qa.Manifest;
using Cauce.Infrastructure.Recommendations.Llm;
using Microsoft.EntityFrameworkCore;

namespace Cauce.Infrastructure.Qa.Seeding;

/// <summary>
/// Recomendaciones en el estado del manifiesto, recorriendo las transiciones del agregado con fechas del ancla. Las
/// del motor llevan ítems del catálogo; las manuales, título propio. No escribe outbox ni avisos.
/// </summary>
public sealed class QaRecommendationSeedApplier : IQaSeedApplier
{
    /// <summary>
    /// Texto de una explicación personalizada sembrada; ninguna se genera en vivo.
    /// </summary>
    public const string PersonalizedExplanation =
        "Según tus registros recientes, algunos de los alimentos que consumes tienen un contenido alto de FODMAP y " +
        "podrían relacionarse con tus síntomas. Esta recomendación se apoya en las clasificaciones FODMAP de esos " +
        "alimentos y en tu historial de las últimas semanas.";

    /// <summary>
    /// Nota clínica de aprobación cuando el caso no trae una.
    /// </summary>
    public const string DefaultApprovalNote = "Recomendación revisada y aprobada por el nutricionista.";

    /// <summary>
    /// Motivo de rechazo cuando el caso no trae uno.
    /// </summary>
    public const string DefaultRejectionReason = "No corresponde al cuadro clínico actual del paciente.";

    /// <summary>
    /// Descripción de una recomendación manual cuando el caso no trae una.
    /// </summary>
    public const string DefaultManualDescription = "Indicación del nutricionista para tu plan de alimentación.";

    /// <summary>
    /// Nota clínica de una recomendación manual cuando el caso no trae una.
    /// </summary>
    public const string DefaultManualNote = "Indicación registrada por el nutricionista.";

    /// <summary>
    /// Días de vigencia de una pendiente cuando el caso no los da; no la ventana de 72 horas del motor.
    /// </summary>
    public const int DefaultPendingDays = 10;

    /// <summary>
    /// Días que sigue sin vencer una aprobada y aún no entregada, porque la expiración también se evalúa al leer.
    /// </summary>
    public const int ApprovedUndeliveredDays = 30;

    private static readonly IReadOnlyDictionary<string, decimal> ConfidenceLevels = new Dictionary<string, decimal>(StringComparer.Ordinal)
    {
        ["high"] = 0.85m,
        ["medium"] = 0.60m,
        ["low"] = 0.30m
    };

    private const decimal DefaultConfidence = 0.80m;

    private readonly FallbackExplanationProvider _fallback;

    /// <summary>
    /// Inicializa el aplicador con el proveedor de la explicación de respaldo.
    /// </summary>
    public QaRecommendationSeedApplier(FallbackExplanationProvider fallback)
    {
        _fallback = fallback;
    }

    /// <inheritdoc />
    public int Order => 50;

    /// <inheritdoc />
    public bool CanApply(QaSeed seed) => seed is QaRecommendationSeed;

    /// <inheritdoc />
    public async Task ApplyAsync(QaSeed seed, QaSeedContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        var recommendation = (QaRecommendationSeed)seed;
        var reviewerId = await ReviewerIdAsync(context, ct).ConfigureAwait(false);

        // Cada recomendación del mismo paciente queda un minuto antes que la anterior, para un orden estable.
        var stagger = TimeSpan.FromMinutes(context.Db.Recommendations.Local.Count(existing => existing.PatientId == context.Owner.Id));
        var created = recommendation.IsManual
            ? CreateManual(recommendation, context, reviewerId, stagger)
            : await CreateEngineAsync(recommendation, context, reviewerId, stagger, ct).ConfigureAwait(false);

        context.Db.Recommendations.Add(created);
        AddSupportingHistory(recommendation, context);
    }

    private Recommendation CreateManual(QaRecommendationSeed seed, QaSeedContext context, Guid reviewerId, TimeSpan stagger)
    {
        var anchor = context.Clock.Anchor;
        var createdAt = (seed.ReceivedAgo is null ? anchor.AddHours(-2) : context.Clock.Resolve(seed.ReceivedAgo)) - stagger;
        DateTime? validUntil = seed.ValidDaysLeft is { } days ? anchor.AddDays(days) : null;

        var manual = Recommendation.CreateManual(
            context.Owner.Id,
            reviewerId,
            seed.Title,
            seed.Description ?? DefaultManualDescription,
            seed.Steps.Count > 0 ? seed.Steps : null,
            seed.ClinicalNote ?? DefaultManualNote,
            validUntil,
            createdAt);
        DeliverAndRespond(manual, seed, createdAt, anchor);
        return manual;
    }

    private async Task<Recommendation> CreateEngineAsync(
        QaRecommendationSeed seed, QaSeedContext context, Guid reviewerId, TimeSpan stagger, CancellationToken ct)
    {
        var anchor = context.Clock.Anchor;
        var foods = QaCatalog.EngineFoods(seed);
        var items = foods
            .Select(food => RecommendationItem.Create(context.Catalog.Foods[food.Food], ToActionType(food.Action), Reasoning(food.Action), substituteFoodId: null))
            .ToList();
        var (source, explanation) = Explanation(seed, foods);
        var confidence = ConfidenceScore.Create(seed.Confidence is not null ? ConfidenceLevels[seed.Confidence] : DefaultConfidence);
        var modelVersionId = await context.Db.ModelVersions.Where(version => version.IsActive).Select(version => version.Id)
            .FirstAsync(ct).ConfigureAwait(false);

        var reviewedAt = ReviewedAt(seed, context.Clock) - stagger;
        var generatedAt = seed.Status switch
        {
            QaRecommendationStatus.PendingReview => (seed.PendingSinceAgo is null ? anchor.AddHours(-1) : context.Clock.Resolve(seed.PendingSinceAgo)) - stagger,
            QaRecommendationStatus.Expired => anchor.AddDays(-4) - stagger,
            _ => reviewedAt.AddHours(-2)
        };
        var expiresAt = seed.Status switch
        {
            QaRecommendationStatus.PendingReview => anchor.AddDays(seed.ExpiresInDays ?? DefaultPendingDays),
            QaRecommendationStatus.Expired => anchor.AddDays(-1),
            _ => anchor.AddDays(ApprovedUndeliveredDays)
        };

        var engine = Recommendation.Generate(
            context.Owner.Id, modelVersionId, confidence, source, explanation, items, generatedAt, expiresAt - generatedAt);
        engine.MarkPendingReview(generatedAt);

        switch (seed.Status)
        {
            case QaRecommendationStatus.ApprovedAsGenerated:
                engine.Approve(reviewerId, seed.ClinicalNote ?? DefaultApprovalNote, reviewedAt);
                DeliverAndRespond(engine, seed, reviewedAt, anchor);
                break;
            case QaRecommendationStatus.ApprovedModified:
                engine.ModifyByNutritionist(reviewerId, seed.ClinicalNote ?? DefaultApprovalNote, reviewedAt);
                DeliverAndRespond(engine, seed, reviewedAt, anchor);
                break;
            case QaRecommendationStatus.Rejected:
                engine.Reject(reviewerId, seed.ClinicalNote ?? DefaultRejectionReason, reviewedAt);
                break;
            case QaRecommendationStatus.Expired:
                engine.Expire(anchor);
                break;
        }

        return engine;
    }

    private (ExplanationSource Source, string Text) Explanation(QaRecommendationSeed seed, IReadOnlyList<(string Food, QaItemAction Action)> foods)
    {
        var fallback = seed.ExplanationSource == nameof(ExplanationSource.Fallback) || seed.Explanation == "fallback";
        if (!fallback)
        {
            return (ExplanationSource.LlmGenerated, PersonalizedExplanation);
        }

        var result = _fallback.GenerateFor(foods.Select(food => new ExplanationItem(food.Food, ToActionType(food.Action), null)).ToList());
        return (result.Source, result.Text);
    }

    private static DateTime ReviewedAt(QaRecommendationSeed seed, QaClock clock)
    {
        if (seed.ReceivedAgo is not null)
        {
            return clock.Resolve(seed.ReceivedAgo);
        }

        if (seed.ApprovedInMonth is not null && QaManifestValidator.TryParseMonth(seed.ApprovedInMonth, out var firstDay))
        {
            return QaClock.AtLimaTime(firstDay.AddDays(14), new TimeOnly(12, 0));
        }

        return clock.Anchor.AddHours(-2);
    }

    private static void DeliverAndRespond(Recommendation recommendation, QaRecommendationSeed seed, DateTime approvedAt, DateTime anchor)
    {
        if (!seed.PatientOpened && !seed.PatientResponded)
        {
            return;
        }

        var deliveredAt = Min(approvedAt.AddHours(1), anchor);
        recommendation.Deliver(deliveredAt);
        if (seed.PatientResponded)
        {
            var respondedAt = Min(deliveredAt.AddHours(1), anchor);
            recommendation.RecordFeedback(
                RecommendationFeedback.Submit(recommendation.Id, wasApplied: true, FeedbackOutcome.Improvement, comment: null, SyncStatus.SyncCompleted, respondedAt),
                respondedAt);
        }
    }

    // Historial que respalda la recomendación: comidas del primer alimento a reducir, cada una seguida de dolor.
    private static void AddSupportingHistory(QaRecommendationSeed seed, QaSeedContext context)
    {
        var (count, windowDays) = seed.SupportingData is { } data
            ? (data.PainRecords, data.WindowDays)
            : seed.WithRecentHistory ? (3, 7) : (0, 0);
        if (count == 0)
        {
            return;
        }

        var food = seed.Reduce.FirstOrDefault() ?? seed.Avoid.FirstOrDefault() ?? QaCatalog.DefaultReduceFood;
        for (var index = 0; index < count; index++)
        {
            var mealAt = context.Clock.Anchor.AddDays(-(1 + (index * (windowDays - 1) / count)));
            QaClinicalRecordSeedApplier.AddMeal(context, food, mealAt, QaClinicalRecordSeedApplier.DefaultGrams, MealTime.Lunch);
            QaClinicalRecordSeedApplier.AddSymptom(context, "Dolor abdominal", 60, mealAt.AddHours(2));
        }
    }

    private async Task<Guid> ReviewerIdAsync(QaSeedContext context, CancellationToken ct)
    {
        var patientId = context.Owner.Id;
        var linked = context.Db.NutritionistPatients.Local
            .Where(link => link.PatientId == patientId && link.Status == AssignmentStatus.Active)
            .Select(link => (Guid?)link.NutritionistId)
            .FirstOrDefault()
            ?? await context.Db.NutritionistPatients
                .Where(link => link.PatientId == patientId && link.Status == AssignmentStatus.Active)
                .Select(link => (Guid?)link.NutritionistId)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);
        if (linked is { } nutritionistId)
        {
            return nutritionistId;
        }

        // Sin vínculo, revisa el primer nutricionista activo del manifiesto: determinista y sin datos de un caso.
        var fallbackEmail = context.Manifest.SeededAccounts
            .First(account => account is { Role: QaRole.Nutritionist, Status: QaAccountStatus.Active }).Email;
        return (await context.RequireUserAsync(fallbackEmail, ct).ConfigureAwait(false)).Id;
    }

    private static string Reasoning(QaItemAction action) => action == QaItemAction.Suggest
        ? "Contenido bajo de FODMAP."
        : "Contenido alto de FODMAP.";

    private static ActionType ToActionType(QaItemAction action) => action switch
    {
        QaItemAction.Suggest => ActionType.Suggest,
        QaItemAction.Avoid => ActionType.Avoid,
        _ => ActionType.Reduce
    };

    private static DateTime Min(DateTime first, DateTime second) => first < second ? first : second;
}
