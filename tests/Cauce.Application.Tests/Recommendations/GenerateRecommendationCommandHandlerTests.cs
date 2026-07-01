using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Common.Interfaces.Recommendations;
using Cauce.Application.Recommendations.Configuration;
using Cauce.Application.Recommendations.Contracts;
using Cauce.Application.Recommendations.UseCases.GenerateRecommendation;
using Cauce.Domain.ClinicalRegistry.Enums;
using Cauce.Domain.Identity;
using Cauce.Domain.Recommendations;
using Cauce.Domain.Recommendations.Enums;
using Cauce.Domain.Recommendations.Exceptions;
using Cauce.Domain.Recommendations.Services;
using Cauce.Domain.Recommendations.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Cauce.Application.Tests.Recommendations;

/// <summary>
/// Pruebas del handler de generación de recomendaciones, con foco en el guardrail de alergias
/// (DEC-B4-09), la selección de sustituto (DEC-B4-13) y la auto-aprobación (DEC-B4-04).
/// </summary>
public sealed class GenerateRecommendationCommandHandlerTests
{
    private const int PatientRoleId = 1;
    private static readonly DateTime Now = DateTime.UtcNow;
    private static readonly ModelVersionDescriptor Descriptor = new("rule-v1.0.0", "HASH", "Rule");

    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IPatientProfileReader _profileReader = Substitute.For<IPatientProfileReader>();
    private readonly IPatientClinicalHistoryReader _historyReader = Substitute.For<IPatientClinicalHistoryReader>();
    private readonly IPatientAllergyReader _allergyReader = Substitute.For<IPatientAllergyReader>();
    private readonly IModelVersionRepository _modelVersionRepository = Substitute.For<IModelVersionRepository>();
    private readonly IRecommendationEngine _engine = Substitute.For<IRecommendationEngine>();
    private readonly IExplanationOrchestrator _orchestrator = Substitute.For<IExplanationOrchestrator>();
    private readonly IRecommendationRepository _recommendationRepository = Substitute.For<IRecommendationRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ILogger<GenerateRecommendationCommandHandler> _logger =
        Substitute.For<ILogger<GenerateRecommendationCommandHandler>>();
    private readonly List<Recommendation> _added = new();

    public GenerateRecommendationCommandHandlerTests()
    {
        ArrangePatient();
        _profileReader.GetPatientContextAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new PatientContextSnapshot(30, "Masculino", 22.5m, "SII-M", 2, "Media", false, false));
        _modelVersionRepository.GetActiveAsync(Arg.Any<CancellationToken>())
            .Returns(ModelVersion.Register("rule-v1.0.0", "HASH", 250_000, "{}", "seeder", Now));
        _allergyReader.GetAllergyFoodIdsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<Guid>());
        _orchestrator.ExplainAsync(Arg.Any<PatientContextSnapshot>(), Arg.Any<IReadOnlyList<ExplanationItem>>(), Arg.Any<CancellationToken>())
            .Returns(new ExplanationResult("Esta es una explicación de prueba suficientemente larga para el paciente.", ExplanationSource.LlmGenerated));
        _recommendationRepository.AddAsync(Arg.Do<Recommendation>(r => _added.Add(r)), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
    }

    private void ArrangePatient()
    {
        var user = User.CreatePatient(Guid.NewGuid(), "kc", "p@cauce.local", "Paciente", PatientRoleId);
        _currentUser.UserId.Returns(Guid.NewGuid());
        _userRepository.FindByKeycloakIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(user);
        _userRepository.GetRoleIdAsync(UserRoles.Patient, Arg.Any<CancellationToken>()).Returns(PatientRoleId);
    }

    private static CandidateFood Candidate(Guid id, string category = "frutas") =>
        new(id, "Alimento " + id.ToString()[..4], category, FodmapLevel.Low, 0, 0, 0, 0, 100);

    private void ArrangeCandidates(IReadOnlyList<CandidateFood> candidates)
    {
        _historyReader.GetCandidateFoodsAsync(Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(candidates);
    }

    private void ArrangeEngine(IReadOnlyDictionary<Guid, decimal> scoreByFood, decimal confidence)
    {
        _engine.Descriptor.Returns(Descriptor);
        _engine.ScoreFoodsAsync(Arg.Any<RecommendationEngineInput>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var input = callInfo.Arg<RecommendationEngineInput>();
                var scores = input.CandidateFoods
                    .Select(food => new FoodScore(food.FoodId, scoreByFood.GetValueOrDefault(food.FoodId, 0.30m), "razón"))
                    .ToList();
                return Task.FromResult(new RecommendationEngineResult(scores, ConfidenceScore.Create(confidence), Descriptor));
            });
    }

    private GenerateRecommendationCommandHandler CreateHandler(RecommendationsOptions? options = null) => new(
        _currentUser, _userRepository, _profileReader, _historyReader, _allergyReader, _modelVersionRepository,
        _engine, _orchestrator, new AutoApprovalGuard(), _recommendationRepository, _unitOfWork,
        Options.Create(options ?? new RecommendationsOptions()), _logger);

    private static IReadOnlyList<Guid> NewIds(int count) =>
        Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToList();

    [Fact]
    public async Task Handle_HappyPath_PersistsPendingReview()
    {
        var ids = NewIds(5);
        ArrangeCandidates(ids.Select(id => Candidate(id)).ToList());
        ArrangeEngine(ids.ToDictionary(id => id, _ => 0.30m), confidence: 0m);

        var result = await CreateHandler().Handle(new GenerateRecommendationCommand(Guid.NewGuid()), CancellationToken.None);

        result.Status.Should().Be(RecommendationStatus.PendingReview);
        result.RequiresReview.Should().BeTrue();
        _added.Single().Status.Should().Be(RecommendationStatus.PendingReview);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithInsufficientHistory_Throws()
    {
        ArrangeCandidates(new[] { Candidate(Guid.NewGuid()), Candidate(Guid.NewGuid()) });

        var act = () => CreateHandler().Handle(new GenerateRecommendationCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<InsufficientClinicalHistoryException>();
    }

    [Fact]
    public async Task Handle_WithAllergiesFilteringAllCandidates_Throws()
    {
        var ids = NewIds(5);
        ArrangeCandidates(ids.Select(id => Candidate(id)).ToList());
        _allergyReader.GetAllergyFoodIdsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<Guid>(ids));

        var act = () => CreateHandler().Handle(new GenerateRecommendationCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<AllCandidatesFilteredByAllergiesException>();
    }

    [Fact]
    public async Task Handle_AllergyFilter_NeverIncludesAllergenInItems()
    {
        var allergen = Guid.NewGuid();
        var safe = NewIds(5);
        var all = new List<CandidateFood> { Candidate(allergen) };
        all.AddRange(safe.Select(id => Candidate(id)));
        ArrangeCandidates(all);
        _allergyReader.GetAllergyFoodIdsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<Guid> { allergen });
        ArrangeEngine(safe.ToDictionary(id => id, _ => 0.90m), confidence: 0.9m);

        await CreateHandler().Handle(new GenerateRecommendationCommand(Guid.NewGuid()), CancellationToken.None);

        _added.Single().Items.Should().NotContain(item => item.FoodId == allergen);
        _added.Single().Items.Should().NotContain(item => item.SubstituteFoodId == allergen);
    }

    [Fact]
    public async Task Handle_WithAutoApprovalEnabled_AndCriteriaMet_AutoApproves()
    {
        var ids = NewIds(5);
        ArrangeCandidates(ids.Select(id => Candidate(id)).ToList());
        ArrangeEngine(ids.ToDictionary(id => id, _ => 0.30m), confidence: 0.99m);
        var options = new RecommendationsOptions { AutoApprovalEnabled = true, AutoApprovalThreshold = 0.90m };

        var result = await CreateHandler(options).Handle(new GenerateRecommendationCommand(Guid.NewGuid()), CancellationToken.None);

        result.Status.Should().Be(RecommendationStatus.Approved);
        result.RequiresReview.Should().BeFalse();
        _added.Single().AutoApproved.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenEngineThrows_PropagatesException()
    {
        var ids = NewIds(5);
        ArrangeCandidates(ids.Select(id => Candidate(id)).ToList());
        _engine.Descriptor.Returns(Descriptor);
        _engine.ScoreFoodsAsync(Arg.Any<RecommendationEngineInput>(), Arg.Any<CancellationToken>())
            .Returns<Task<RecommendationEngineResult>>(_ => throw new InvalidOperationException("engine failure"));

        var act = () => CreateHandler().Handle(new GenerateRecommendationCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Handle_WhenExplanationReturnsFallback_PersistsWithFallbackSource()
    {
        var ids = NewIds(5);
        ArrangeCandidates(ids.Select(id => Candidate(id)).ToList());
        ArrangeEngine(ids.ToDictionary(id => id, _ => 0.30m), confidence: 0m);
        _orchestrator.ExplainAsync(Arg.Any<PatientContextSnapshot>(), Arg.Any<IReadOnlyList<ExplanationItem>>(), Arg.Any<CancellationToken>())
            .Returns(new ExplanationResult("Texto de respaldo generado por la plantilla estática.", ExplanationSource.Fallback));

        await CreateHandler().Handle(new GenerateRecommendationCommand(Guid.NewGuid()), CancellationToken.None);

        _added.Single().ExplanationSource.Should().Be(ExplanationSource.Fallback);
    }

    [Fact]
    public async Task Handle_AvoidItem_WithValidSubstituteCandidate_PromotesToSubstitute()
    {
        var avoidId = Guid.NewGuid();
        var substituteId = Guid.NewGuid();
        var fillers = NewIds(3);
        var candidates = new List<CandidateFood> { Candidate(avoidId, "frutas"), Candidate(substituteId, "frutas") };
        candidates.AddRange(fillers.Select(id => Candidate(id, "otros")));
        ArrangeCandidates(candidates);

        var scores = new Dictionary<Guid, decimal> { [avoidId] = 0.90m, [substituteId] = 0.20m };
        foreach (var id in fillers)
        {
            scores[id] = 0.30m;
        }

        ArrangeEngine(scores, confidence: 0.9m);

        await CreateHandler().Handle(new GenerateRecommendationCommand(Guid.NewGuid()), CancellationToken.None);

        var avoidItem = _added.Single().Items.Single(item => item.FoodId == avoidId);
        avoidItem.ActionType.Should().Be(ActionType.Substitute);
        avoidItem.SubstituteFoodId.Should().Be(substituteId);
    }

    [Fact]
    public async Task Handle_AvoidItem_WithNoSubstituteInSameCategory_RemainsAvoid()
    {
        var avoidId = Guid.NewGuid();
        var fillers = NewIds(4);
        var candidates = new List<CandidateFood> { Candidate(avoidId, "frutas") };
        candidates.AddRange(fillers.Select(id => Candidate(id, "otros")));
        ArrangeCandidates(candidates);

        var scores = new Dictionary<Guid, decimal> { [avoidId] = 0.90m };
        foreach (var id in fillers)
        {
            scores[id] = 0.20m; // seguros, pero de otra categoría
        }

        ArrangeEngine(scores, confidence: 0.9m);

        await CreateHandler().Handle(new GenerateRecommendationCommand(Guid.NewGuid()), CancellationToken.None);

        var avoidItem = _added.Single().Items.Single(item => item.FoodId == avoidId);
        avoidItem.ActionType.Should().Be(ActionType.Avoid);
        avoidItem.SubstituteFoodId.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ExpandsCandidateWindow_WhenInsufficientInInitialWindow()
    {
        var few = NewIds(3);
        var enough = NewIds(5);
        _historyReader.GetCandidateFoodsAsync(Arg.Any<Guid>(), 14, Arg.Any<CancellationToken>())
            .Returns(few.Select(id => Candidate(id)).ToList());
        _historyReader.GetCandidateFoodsAsync(Arg.Any<Guid>(), 30, Arg.Any<CancellationToken>())
            .Returns(enough.Select(id => Candidate(id)).ToList());
        ArrangeEngine(enough.ToDictionary(id => id, _ => 0.30m), confidence: 0m);

        var result = await CreateHandler().Handle(new GenerateRecommendationCommand(Guid.NewGuid()), CancellationToken.None);

        result.Status.Should().Be(RecommendationStatus.PendingReview);
        _added.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_WhenNoActiveModelVersion_Throws()
    {
        var ids = NewIds(5);
        ArrangeCandidates(ids.Select(id => Candidate(id)).ToList());
        ArrangeEngine(ids.ToDictionary(id => id, _ => 0.30m), confidence: 0m);
        _modelVersionRepository.GetActiveAsync(Arg.Any<CancellationToken>()).Returns((ModelVersion?)null);

        var act = () => CreateHandler().Handle(new GenerateRecommendationCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NoActiveModelVersionException>();
    }
}
