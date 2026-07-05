using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Common.Interfaces.Recommendations;
using Cauce.Application.Recommendations.UseCases.SubmitFeedback;
using Cauce.Domain.Identity;
using Cauce.Domain.Recommendations;
using Cauce.Domain.Recommendations.Enums;
using Cauce.Domain.Recommendations.Exceptions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Cauce.Application.Tests.Recommendations;

/// <summary>
/// Pruebas del handler de envío de retroalimentación (ownership y estado entregado).
/// </summary>
public sealed class SubmitFeedbackCommandHandlerTests
{
    private const int PatientRoleId = 1;
    private static readonly DateTime Now = DateTime.UtcNow;

    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IRecommendationRepository _recommendationRepository = Substitute.For<IRecommendationRepository>();
    private readonly IOutboxWriter _outboxWriter = Substitute.For<IOutboxWriter>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ILogger<SubmitFeedbackCommandHandler> _logger =
        Substitute.For<ILogger<SubmitFeedbackCommandHandler>>();

    private readonly Guid _patientId;

    public SubmitFeedbackCommandHandlerTests()
    {
        var patient = User.CreatePatient(Guid.NewGuid(), "kc", "p@cauce.local", "Paciente", PatientRoleId);
        _patientId = patient.Id;
        _currentUser.UserId.Returns(Guid.NewGuid());
        _userRepository.FindByKeycloakIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(patient);
        _userRepository.GetRoleIdAsync(UserRoles.Patient, Arg.Any<CancellationToken>()).Returns(PatientRoleId);
    }

    private SubmitFeedbackCommandHandler CreateHandler() =>
        new(_currentUser, _userRepository, _recommendationRepository, _outboxWriter, _unitOfWork, _logger);

    private static SubmitFeedbackCommand Command(Guid id) =>
        new(id, WasApplied: true, FeedbackOutcome.Improvement, "todo bien", Guid.NewGuid());

    [Fact]
    public async Task Handle_FromDelivered_RecordsFeedback()
    {
        var recommendation = RecommendationTestData.Delivered(_patientId, Now);
        _recommendationRepository.GetByIdAsync(recommendation.Id, Arg.Any<CancellationToken>()).Returns(recommendation);

        await CreateHandler().Handle(Command(recommendation.Id), CancellationToken.None);

        recommendation.Status.Should().Be(RecommendationStatus.FeedbackReceived);
        recommendation.Feedback.Should().NotBeNull();
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NotOwner_Throws()
    {
        var recommendation = RecommendationTestData.Delivered(Guid.NewGuid(), Now);
        _recommendationRepository.GetByIdAsync(recommendation.Id, Arg.Any<CancellationToken>()).Returns(recommendation);

        var act = () => CreateHandler().Handle(Command(recommendation.Id), CancellationToken.None);

        await act.Should().ThrowAsync<RecommendationAccessDeniedException>();
    }

    [Fact]
    public async Task Handle_FromNonDeliveredState_Throws()
    {
        var recommendation = RecommendationTestData.Approved(_patientId, Now);
        _recommendationRepository.GetByIdAsync(recommendation.Id, Arg.Any<CancellationToken>()).Returns(recommendation);

        var act = () => CreateHandler().Handle(Command(recommendation.Id), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidRecommendationStateTransitionException>();
    }
}
