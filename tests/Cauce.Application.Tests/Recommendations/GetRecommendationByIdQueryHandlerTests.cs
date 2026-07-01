using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Common.Interfaces.Patients;
using Cauce.Application.Common.Interfaces.Recommendations;
using Cauce.Application.Recommendations.Contracts;
using Cauce.Application.Recommendations.UseCases.GetRecommendationById;
using Cauce.Domain.Identity;
using Cauce.Domain.Recommendations;
using Cauce.Domain.Recommendations.Enums;
using Cauce.Domain.Recommendations.Exceptions;
using FluentAssertions;
using NSubstitute;

namespace Cauce.Application.Tests.Recommendations;

/// <summary>
/// Pruebas del handler de detalle de recomendación (autorización por rol y expiración on-read).
/// </summary>
public sealed class GetRecommendationByIdQueryHandlerTests
{
    private const int PatientRoleId = 1;
    private const int NutritionistRoleId = 2;
    private static readonly DateTime Now = DateTime.UtcNow;

    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IRecommendationRepository _recommendationRepository = Substitute.For<IRecommendationRepository>();
    private readonly INutritionistPatientRepository _assignments = Substitute.For<INutritionistPatientRepository>();
    private readonly IModelVersionRepository _modelVersionRepository = Substitute.For<IModelVersionRepository>();
    private readonly IFoodCatalogReader _foodCatalogReader = Substitute.For<IFoodCatalogReader>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private readonly Guid _patientId;

    public GetRecommendationByIdQueryHandlerTests()
    {
        var patient = User.CreatePatient(Guid.NewGuid(), "kc", "p@cauce.local", "Paciente", PatientRoleId);
        _patientId = patient.Id;
        _currentUser.UserId.Returns(Guid.NewGuid());
        _userRepository.FindByKeycloakIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(patient);
        _userRepository.GetRoleIdAsync(UserRoles.Patient, Arg.Any<CancellationToken>()).Returns(PatientRoleId);
        _userRepository.GetRoleIdAsync(UserRoles.Nutritionist, Arg.Any<CancellationToken>()).Returns(NutritionistRoleId);
        _modelVersionRepository.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(ModelVersion.Register("rule-v1.0.0", "HASH", 250_000, "{}", "seeder", Now));
        _foodCatalogReader.GetFoodNamesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, FoodNameInfo>());
    }

    private GetRecommendationByIdQueryHandler CreateHandler() => new(
        _currentUser, _userRepository, _recommendationRepository, _assignments,
        _modelVersionRepository, _foodCatalogReader, _unitOfWork);

    [Fact]
    public async Task Handle_AsOwnerPatient_ReturnsDto()
    {
        var recommendation = RecommendationTestData.PendingReview(_patientId, Now);
        _recommendationRepository.GetByIdWithDetailsAsync(recommendation.Id, Arg.Any<CancellationToken>()).Returns(recommendation);

        var dto = await CreateHandler().Handle(new GetRecommendationByIdQuery(recommendation.Id), CancellationToken.None);

        dto.RecommendationId.Should().Be(recommendation.Id);
        dto.ModelVersionName.Should().Be("rule-v1.0.0");
    }

    [Fact]
    public async Task Handle_WhenExpiresAtPast_AndStatusEligible_TransitsToExpired()
    {
        var recommendation = RecommendationTestData.PendingReview(_patientId, Now.AddHours(-100));
        _recommendationRepository.GetByIdWithDetailsAsync(recommendation.Id, Arg.Any<CancellationToken>()).Returns(recommendation);

        var dto = await CreateHandler().Handle(new GetRecommendationByIdQuery(recommendation.Id), CancellationToken.None);

        dto.Status.Should().Be(RecommendationStatus.Expired);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenNotFound_Throws()
    {
        _recommendationRepository.GetByIdWithDetailsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Recommendation?)null);

        var act = () => CreateHandler().Handle(new GetRecommendationByIdQuery(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<RecommendationNotFoundException>();
    }

    [Fact]
    public async Task Handle_PatientNotOwner_Throws()
    {
        var recommendation = RecommendationTestData.PendingReview(Guid.NewGuid(), Now);
        _recommendationRepository.GetByIdWithDetailsAsync(recommendation.Id, Arg.Any<CancellationToken>()).Returns(recommendation);

        var act = () => CreateHandler().Handle(new GetRecommendationByIdQuery(recommendation.Id), CancellationToken.None);

        await act.Should().ThrowAsync<RecommendationAccessDeniedException>();
    }
}
