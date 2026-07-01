using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Common.Interfaces.Patients;
using Cauce.Application.Common.Interfaces.Recommendations;
using Cauce.Application.Recommendations.UseCases.ApproveRecommendation;
using Cauce.Domain.Identity;
using Cauce.Domain.Recommendations;
using Cauce.Domain.Recommendations.Enums;
using Cauce.Domain.Recommendations.Exceptions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Cauce.Application.Tests.Recommendations;

/// <summary>
/// Pruebas del handler de aprobación de recomendaciones (autorización por asignación y estado).
/// </summary>
public sealed class ApproveRecommendationCommandHandlerTests
{
    private const int NutritionistRoleId = 2;
    private static readonly DateTime Now = DateTime.UtcNow;

    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IRecommendationRepository _recommendationRepository = Substitute.For<IRecommendationRepository>();
    private readonly INutritionistPatientRepository _assignments = Substitute.For<INutritionistPatientRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ILogger<ApproveRecommendationCommandHandler> _logger =
        Substitute.For<ILogger<ApproveRecommendationCommandHandler>>();

    private readonly Guid _nutritionistId;
    private readonly Guid _patientId = Guid.NewGuid();

    public ApproveRecommendationCommandHandlerTests()
    {
        var nutritionist = User.CreateNutritionist(Guid.NewGuid(), "kc", "n@cauce.local", "Nutri", NutritionistRoleId);
        _nutritionistId = nutritionist.Id;
        _currentUser.UserId.Returns(Guid.NewGuid());
        _userRepository.FindByKeycloakIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(nutritionist);
        _userRepository.GetRoleIdAsync(UserRoles.Nutritionist, Arg.Any<CancellationToken>()).Returns(NutritionistRoleId);
    }

    private ApproveRecommendationCommandHandler CreateHandler() =>
        new(_currentUser, _userRepository, _recommendationRepository, _assignments, _unitOfWork, _logger);

    private static ApproveRecommendationCommand Command(Guid id) => new(id, "nota clínica válida y suficiente", Guid.NewGuid());

    [Fact]
    public async Task Handle_FromPendingReview_TransitsToApproved()
    {
        var recommendation = RecommendationTestData.PendingReview(_patientId, Now);
        _recommendationRepository.GetByIdAsync(recommendation.Id, Arg.Any<CancellationToken>()).Returns(recommendation);
        _assignments.ActiveAssignmentExistsAsync(_nutritionistId, _patientId, Arg.Any<CancellationToken>()).Returns(true);

        await CreateHandler().Handle(Command(recommendation.Id), CancellationToken.None);

        recommendation.Status.Should().Be(RecommendationStatus.Approved);
        recommendation.ReviewedByNutritionistId.Should().Be(_nutritionistId);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NutritionistNotAssigned_Throws()
    {
        var recommendation = RecommendationTestData.PendingReview(_patientId, Now);
        _recommendationRepository.GetByIdAsync(recommendation.Id, Arg.Any<CancellationToken>()).Returns(recommendation);
        _assignments.ActiveAssignmentExistsAsync(_nutritionistId, _patientId, Arg.Any<CancellationToken>()).Returns(false);

        var act = () => CreateHandler().Handle(Command(recommendation.Id), CancellationToken.None);

        await act.Should().ThrowAsync<RecommendationAccessDeniedException>();
    }

    [Fact]
    public async Task Handle_NotFound_Throws()
    {
        _recommendationRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Recommendation?)null);

        var act = () => CreateHandler().Handle(Command(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<RecommendationNotFoundException>();
    }

    [Fact]
    public async Task Handle_FromApprovedState_Throws()
    {
        var recommendation = RecommendationTestData.Approved(_patientId, Now);
        _recommendationRepository.GetByIdAsync(recommendation.Id, Arg.Any<CancellationToken>()).Returns(recommendation);
        _assignments.ActiveAssignmentExistsAsync(_nutritionistId, _patientId, Arg.Any<CancellationToken>()).Returns(true);

        var act = () => CreateHandler().Handle(Command(recommendation.Id), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidRecommendationStateTransitionException>();
    }
}
