using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Common.Interfaces.Recommendations;
using Cauce.Application.Recommendations.UseCases.DeliverRecommendation;
using Cauce.Domain.Identity;
using Cauce.Domain.Recommendations;
using Cauce.Domain.Recommendations.Enums;
using Cauce.Domain.Recommendations.Exceptions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Cauce.Application.Tests.Recommendations;

/// <summary>
/// Pruebas del handler de entrega de recomendaciones (ownership y expiración on-read).
/// </summary>
public sealed class DeliverRecommendationCommandHandlerTests
{
    private const int PatientRoleId = 1;
    private static readonly DateTime Now = DateTime.UtcNow;

    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IRecommendationRepository _recommendationRepository = Substitute.For<IRecommendationRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ILogger<DeliverRecommendationCommandHandler> _logger =
        Substitute.For<ILogger<DeliverRecommendationCommandHandler>>();

    private readonly Guid _patientId;

    public DeliverRecommendationCommandHandlerTests()
    {
        var patient = User.CreatePatient(Guid.NewGuid(), "kc", "p@cauce.local", "Paciente", PatientRoleId);
        _patientId = patient.Id;
        _currentUser.UserId.Returns(Guid.NewGuid());
        _userRepository.FindByKeycloakIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(patient);
        _userRepository.GetRoleIdAsync(UserRoles.Patient, Arg.Any<CancellationToken>()).Returns(PatientRoleId);
    }

    private DeliverRecommendationCommandHandler CreateHandler() =>
        new(_currentUser, _userRepository, _recommendationRepository, _unitOfWork, _logger);

    [Fact]
    public async Task Handle_FromApproved_Delivers()
    {
        var recommendation = RecommendationTestData.Approved(_patientId, Now);
        _recommendationRepository.GetByIdAsync(recommendation.Id, Arg.Any<CancellationToken>()).Returns(recommendation);

        await CreateHandler().Handle(new DeliverRecommendationCommand(recommendation.Id, Guid.NewGuid()), CancellationToken.None);

        recommendation.Status.Should().Be(RecommendationStatus.Delivered);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NotOwner_Throws()
    {
        var recommendation = RecommendationTestData.Approved(Guid.NewGuid(), Now);
        _recommendationRepository.GetByIdAsync(recommendation.Id, Arg.Any<CancellationToken>()).Returns(recommendation);

        var act = () => CreateHandler().Handle(new DeliverRecommendationCommand(recommendation.Id, Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<RecommendationAccessDeniedException>();
    }

    [Fact]
    public async Task Handle_Expired_ExpiresAndThrows()
    {
        var recommendation = RecommendationTestData.Approved(_patientId, Now.AddHours(-100));
        _recommendationRepository.GetByIdAsync(recommendation.Id, Arg.Any<CancellationToken>()).Returns(recommendation);

        var act = () => CreateHandler().Handle(new DeliverRecommendationCommand(recommendation.Id, Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<RecommendationExpiredException>();
        recommendation.Status.Should().Be(RecommendationStatus.Expired);
    }
}
