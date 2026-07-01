using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Common.Interfaces.Recommendations;
using Cauce.Application.Recommendations.Configuration;
using Cauce.Application.Recommendations.Contracts;
using Cauce.Domain.Patients.Exceptions;
using Cauce.Domain.Recommendations;
using Cauce.Domain.Recommendations.Enums;
using Cauce.Domain.Recommendations.Exceptions;
using Cauce.Domain.Recommendations.Services;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cauce.Application.Recommendations.UseCases.GenerateRecommendation;

/// <summary>
/// Handler de la generación de recomendaciones. Orquesta la obtención de candidatos (con
/// expansión de ventana), el filtro duro de alergias previo a la inferencia (DEC-B4-09), la
/// invocación del motor, la traducción de puntajes a acciones con selección de sustituto
/// (DEC-B4-13), la explicación LLM y el guard de auto-aprobación (DEC-B4-04). La cantidad de
/// dependencias es propia de un caso de uso de orquestación.
/// </summary>
public sealed class GenerateRecommendationCommandHandler
    : IRequestHandler<GenerateRecommendationCommand, GenerateRecommendationResult>
{
    private const int ExpandedWindowDays = 30;

    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly IPatientProfileReader _patientProfileReader;
    private readonly IPatientClinicalHistoryReader _historyReader;
    private readonly IPatientAllergyReader _allergyReader;
    private readonly IModelVersionRepository _modelVersionRepository;
    private readonly IRecommendationEngine _engine;
    private readonly IExplanationOrchestrator _explanationOrchestrator;
    private readonly AutoApprovalGuard _autoApprovalGuard;
    private readonly IRecommendationRepository _recommendationRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly RecommendationsOptions _options;
    private readonly ILogger<GenerateRecommendationCommandHandler> _logger;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    public GenerateRecommendationCommandHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        IPatientProfileReader patientProfileReader,
        IPatientClinicalHistoryReader historyReader,
        IPatientAllergyReader allergyReader,
        IModelVersionRepository modelVersionRepository,
        IRecommendationEngine engine,
        IExplanationOrchestrator explanationOrchestrator,
        AutoApprovalGuard autoApprovalGuard,
        IRecommendationRepository recommendationRepository,
        IUnitOfWork unitOfWork,
        IOptions<RecommendationsOptions> options,
        ILogger<GenerateRecommendationCommandHandler> logger)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _patientProfileReader = patientProfileReader;
        _historyReader = historyReader;
        _allergyReader = allergyReader;
        _modelVersionRepository = modelVersionRepository;
        _engine = engine;
        _explanationOrchestrator = explanationOrchestrator;
        _autoApprovalGuard = autoApprovalGuard;
        _recommendationRepository = recommendationRepository;
        _unitOfWork = unitOfWork;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<GenerateRecommendationResult> Handle(
        GenerateRecommendationCommand request,
        CancellationToken cancellationToken)
    {
        _ = request;
        var now = DateTime.UtcNow;
        var patientId = await RecommendationsUserContext
            .ResolvePatientIdAsync(_currentUserService, _userRepository, cancellationToken)
            .ConfigureAwait(false);

        var allergyFoodIds = await _allergyReader.GetAllergyFoodIdsAsync(patientId, cancellationToken).ConfigureAwait(false);
        var candidates = await ResolveSafeCandidatesAsync(patientId, allergyFoodIds, cancellationToken).ConfigureAwait(false);

        var modelVersion = await _modelVersionRepository.GetActiveAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new NoActiveModelVersionException();

        var patientContext = await _patientProfileReader.GetPatientContextAsync(patientId, cancellationToken).ConfigureAwait(false)
            ?? throw new PatientProfileNotFoundException();

        var engineInput = new RecommendationEngineInput(patientContext, candidates, new MealContext("almuerzo"));
        var engineResult = await _engine.ScoreFoodsAsync(engineInput, cancellationToken).ConfigureAwait(false);

        var candidatesByFood = candidates.ToDictionary(candidate => candidate.FoodId);
        var items = BuildItems(engineResult.Scores, candidatesByFood, allergyFoodIds);

        var explanationItems = items
            .Select(item => new ExplanationItem(candidatesByFood[item.FoodId].Name, item.ActionType, item.Reasoning))
            .ToList();
        var explanation = await _explanationOrchestrator
            .ExplainAsync(patientContext, explanationItems, cancellationToken)
            .ConfigureAwait(false);

        var recommendation = Recommendation.Generate(
            patientId,
            modelVersion.Id,
            engineResult.AggregateConfidence,
            explanation.Source,
            explanation.Text,
            items,
            now,
            TimeSpan.FromHours(_options.ExpirationWindowHours));

        var decision = _autoApprovalGuard.Evaluate(
            _options.AutoApprovalEnabled,
            _options.AutoApprovalThreshold,
            _options.MaxAvoidItemsForAutoApproval,
            engineResult.AggregateConfidence,
            items);

        if (decision.ShouldAutoApprove)
        {
            recommendation.AutoApprove(now);
        }
        else
        {
            recommendation.MarkPendingReview(now);
        }

        await _recommendationRepository.AddAsync(recommendation, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Recommendation {RecommendationId} generated for patient {PatientId} with status {Status} (explanation: {ExplanationSource}).",
            recommendation.Id,
            patientId,
            recommendation.Status,
            recommendation.ExplanationSource);

        return new GenerateRecommendationResult(
            recommendation.Id,
            recommendation.Status,
            recommendation.Status == RecommendationStatus.PendingReview,
            recommendation.GeneratedAt,
            recommendation.ExpiresAt);
    }

    private async Task<IReadOnlyList<CandidateFood>> ResolveSafeCandidatesAsync(
        Guid patientId,
        HashSet<Guid> allergyFoodIds,
        CancellationToken ct)
    {
        var candidates = await _historyReader
            .GetCandidateFoodsAsync(patientId, _options.CandidateFoodsWindowDays, ct)
            .ConfigureAwait(false);

        if (candidates.Count < _options.MinCandidateFoodsCount)
        {
            candidates = await _historyReader
                .GetCandidateFoodsAsync(patientId, ExpandedWindowDays, ct)
                .ConfigureAwait(false);
        }

        if (candidates.Count < _options.MinCandidateFoodsCount)
        {
            throw new InsufficientClinicalHistoryException(patientId);
        }

        // Guardrail duro pre-inferencia: los alérgenos se excluyen antes de puntuar (DEC-B4-09).
        var safeCandidates = candidates.Where(candidate => !allergyFoodIds.Contains(candidate.FoodId)).ToList();
        if (safeCandidates.Count == 0)
        {
            throw new AllCandidatesFilteredByAllergiesException(patientId);
        }

        return safeCandidates;
    }

    private List<RecommendationItem> BuildItems(
        IReadOnlyList<FoodScore> scores,
        IReadOnlyDictionary<Guid, CandidateFood> candidatesByFood,
        HashSet<Guid> allergyFoodIds)
    {
        var items = new List<RecommendationItem>(scores.Count);

        foreach (var score in scores)
        {
            var action = ClassifyAction(score.SymptomProbability);
            Guid? substituteFoodId = null;

            if (action == ActionType.Avoid)
            {
                var substitute = FindSubstitute(score, scores, candidatesByFood, allergyFoodIds);
                if (substitute is not null)
                {
                    action = ActionType.Substitute;
                    substituteFoodId = substitute.FoodId;
                }
            }

            items.Add(RecommendationItem.Create(score.FoodId, action, score.Reasoning, substituteFoodId));
        }

        return items;
    }

    private ActionType ClassifyAction(decimal symptomProbability)
    {
        if (symptomProbability >= _options.AvoidThreshold)
        {
            return ActionType.Avoid;
        }

        return symptomProbability >= _options.ReduceThreshold
            ? ActionType.Reduce
            : ActionType.Suggest;
    }

    private FoodScore? FindSubstitute(
        FoodScore original,
        IReadOnlyList<FoodScore> scores,
        IReadOnlyDictionary<Guid, CandidateFood> candidatesByFood,
        HashSet<Guid> allergyFoodIds)
    {
        var originalCategory = candidatesByFood[original.FoodId].Category;

        return scores
            .Where(score => score.FoodId != original.FoodId)
            .Where(score => !allergyFoodIds.Contains(score.FoodId))
            .Where(score => score.SymptomProbability < _options.ReduceThreshold)
            .Where(score => candidatesByFood.TryGetValue(score.FoodId, out var candidate)
                && string.Equals(candidate.Category, originalCategory, StringComparison.Ordinal))
            .OrderBy(score => score.SymptomProbability)
            .FirstOrDefault();
    }
}
