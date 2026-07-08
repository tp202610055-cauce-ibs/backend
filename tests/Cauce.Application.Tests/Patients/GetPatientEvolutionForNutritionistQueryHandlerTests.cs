using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.ClinicalRegistry;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Common.Interfaces.Patients;
using Cauce.Application.Common.Interfaces.Recommendations;
using Cauce.Application.Patients.UseCases.GetPatientEvolutionForNutritionist;
using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry.Enums;
using Cauce.Domain.Identity;
using Cauce.Domain.Patients.Exceptions;
using FluentAssertions;
using NSubstitute;

namespace Cauce.Application.Tests.Patients;

/// <summary>
/// Pruebas del handler <see cref="GetPatientEvolutionForNutritionistQueryHandler"/> (US21).
/// </summary>
public sealed class GetPatientEvolutionForNutritionistQueryHandlerTests
{
    private const int NutritionistRoleId = 2;

    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly INutritionistPatientRepository _nutritionistPatientRepository = Substitute.For<INutritionistPatientRepository>();
    private readonly IIbsSssAssessmentRepository _assessmentRepository = Substitute.For<IIbsSssAssessmentRepository>();
    private readonly IRecommendationSupportingDataReader _supportingDataReader = Substitute.For<IRecommendationSupportingDataReader>();

    private readonly Guid _patientId = Guid.NewGuid();

    private GetPatientEvolutionForNutritionistQueryHandler CreateHandler() => new(
        _currentUserService,
        _userRepository,
        _nutritionistPatientRepository,
        _assessmentRepository,
        _supportingDataReader);

    private void ArrangeNutritionist()
    {
        var nutritionist = User.CreateNutritionist(Guid.NewGuid(), "kc-nutri", "n@cauce.local", "Nutri", NutritionistRoleId);
        _currentUserService.UserId.Returns(Guid.NewGuid());
        _userRepository.FindByKeycloakIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(nutritionist);
        _userRepository.GetRoleIdAsync(UserRoles.Nutritionist, Arg.Any<CancellationToken>()).Returns(NutritionistRoleId);
    }

    private static IbsSssAssessment Baseline(int dim) =>
        IbsSssAssessment.Submit(Guid.NewGuid(), Guid.NewGuid(), AssessmentType.Baseline, 0, dim, dim, dim, dim, dim, DateTime.UtcNow.AddDays(-28));

    private static IbsSssAssessment Periodic(int dim, int cycle) =>
        IbsSssAssessment.Submit(Guid.NewGuid(), Guid.NewGuid(), AssessmentType.Periodic, cycle, dim, dim, dim, dim, dim, DateTime.UtcNow.AddDays(-1));

    [Fact]
    public async Task Handle_AssignedWithSignificantImprovement_ReturnsMetrics()
    {
        ArrangeNutritionist();
        _nutritionistPatientRepository.ActiveAssignmentExistsAsync(Arg.Any<Guid>(), _patientId, Arg.Any<CancellationToken>()).Returns(true);
        var baseline = Baseline(60);   // total 300
        var latest = Periodic(30, 1);  // total 150, mejora de 150 puntos
        _assessmentRepository.ListByPatientAsync(_patientId, Arg.Any<CancellationToken>())
            .Returns(new List<IbsSssAssessment> { baseline, latest });
        _supportingDataReader.GetAsync(_patientId, Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new RecommendationSupportingDataSnapshot(4, 9, new List<string>()));

        var result = await CreateHandler().Handle(new GetPatientEvolutionForNutritionistQuery(_patientId), CancellationToken.None);

        result.IbsSssTimeline.Should().HaveCount(2);
        result.BaselineScore.Should().Be(baseline.TotalScore);
        result.LatestScore.Should().Be(latest.TotalScore);
        result.SignificantClinicalResponse.Should().BeTrue();
        result.PercentChangeFromBaseline.Should().BeNegative();
        result.RegistrationFrequency14d.Should().Be(13);
    }

    [Fact]
    public async Task Handle_NotAssigned_ThrowsForbidden()
    {
        ArrangeNutritionist();
        _nutritionistPatientRepository.ActiveAssignmentExistsAsync(Arg.Any<Guid>(), _patientId, Arg.Any<CancellationToken>()).Returns(false);

        var act = () => CreateHandler().Handle(new GetPatientEvolutionForNutritionistQuery(_patientId), CancellationToken.None);

        await act.Should().ThrowAsync<PatientAccessNotAuthorizedException>();
    }
}
