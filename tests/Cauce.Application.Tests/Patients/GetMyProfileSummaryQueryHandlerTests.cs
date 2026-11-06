using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.ClinicalRegistry;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Common.Interfaces.Patients;
using Cauce.Application.Patients.UseCases.GetMyProfileSummary;
using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry.Enums;
using Cauce.Domain.Identity;
using Cauce.Domain.Patients;
using Cauce.Domain.Patients.Enums;
using FluentAssertions;
using NSubstitute;

namespace Cauce.Application.Tests.Patients;

/// <summary>
/// Pruebas de la fecha de inicio en el piloto de <see cref="GetMyProfileSummaryQueryHandler"/> (US28).
/// </summary>
public sealed class GetMyProfileSummaryQueryHandlerTests
{
    private const int PatientRoleId = 1;

    // 30/10 a las 21:55 de Lima; en UTC ya es 31/10.
    private static readonly DateTime LimaEvening = new(2026, 10, 31, 2, 55, 0, DateTimeKind.Utc);
    private static readonly DateOnly LimaDay = new(2026, 10, 30);

    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IUserRepository _userRepository = Substitute.For<IUserRepository>();
    private readonly IPatientProfileRepository _patientProfileRepository = Substitute.For<IPatientProfileRepository>();
    private readonly INutritionistPatientRepository _nutritionistPatientRepository = Substitute.For<INutritionistPatientRepository>();
    private readonly IIbsSssAssessmentRepository _assessmentRepository = Substitute.For<IIbsSssAssessmentRepository>();
    private readonly IAllergyRepository _allergyRepository = Substitute.For<IAllergyRepository>();
    private readonly IPatientAllergyRepository _patientAllergyRepository = Substitute.For<IPatientAllergyRepository>();

    private GetMyProfileSummaryQueryHandler CreateHandler() => new(
        _currentUserService,
        _userRepository,
        _patientProfileRepository,
        _nutritionistPatientRepository,
        _assessmentRepository,
        _allergyRepository,
        _patientAllergyRepository);

    private User ArrangePatient(DateTime profileCreatedAt, bool onboardingCompleted, IbsSssAssessment? baseline)
    {
        var user = User.CreatePatient(Guid.NewGuid(), "kc", "p@cauce.local", "Paciente", PatientRoleId, PatientCode.FromCorrelative(1));
        var profile = PatientProfile.Create(
            Guid.NewGuid(), user.Id, new DateOnly(1990, 1, 1), BiologicalSex.Female, 60m, 165m, IbsSubtype.IbsM, null, null, profileCreatedAt);
        if (onboardingCompleted)
        {
            profile.CompleteOnboarding(profileCreatedAt);
        }

        _currentUserService.UserId.Returns(Guid.NewGuid());
        _userRepository.FindByKeycloakIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(user);
        _userRepository.GetRoleIdAsync(UserRoles.Patient, Arg.Any<CancellationToken>()).Returns(PatientRoleId);
        _patientProfileRepository.FindByUserIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(profile);
        _assessmentRepository.FindBaselineByPatientAsync(user.Id, Arg.Any<CancellationToken>()).Returns(baseline);
        _assessmentRepository.FindLatestByPatientAsync(user.Id, Arg.Any<CancellationToken>()).Returns(baseline);
        _patientAllergyRepository.ListByPatientAsync(user.Id, Arg.Any<CancellationToken>()).Returns([]);
        _nutritionistPatientRepository.FindActiveByPatientAsync(user.Id, Arg.Any<CancellationToken>()).Returns((Domain.Patients.NutritionistPatient?)null);
        return user;
    }

    [Fact]
    public async Task Handle_BaselineCompletedInTheLimaEvening_StartsThePilotOnThatLimaDay()
    {
        var baseline = IbsSssAssessment.Submit(Guid.NewGuid(), Guid.NewGuid(), AssessmentType.Baseline, 0, 50, 50, 50, 50, 50, LimaEvening);
        ArrangePatient(LimaEvening.AddDays(-3), onboardingCompleted: true, baseline);

        var result = await CreateHandler().Handle(new GetMyProfileSummaryQuery(), CancellationToken.None);

        result.PilotStartDate.Should().Be(LimaDay);
    }

    [Fact]
    public async Task Handle_NoBaselineAndProfileSavedInTheLimaEvening_StartsThePilotOnThatLimaDay()
    {
        ArrangePatient(LimaEvening, onboardingCompleted: true, baseline: null);

        var result = await CreateHandler().Handle(new GetMyProfileSummaryQuery(), CancellationToken.None);

        result.PilotStartDate.Should().Be(LimaDay);
    }

    [Fact]
    public async Task Handle_NoBaselineNorOnboardingAndAccountCreatedInTheLimaEvening_StartsThePilotOnThatLimaDay()
    {
        var user = ArrangePatient(LimaEvening.AddDays(5), onboardingCompleted: false, baseline: null);
        // La cuenta fija su fecha de alta con el reloj real; la prueba la reemplaza para situarla en la franja.
        typeof(User).GetProperty(nameof(User.CreatedAt))!.SetValue(user, LimaEvening);

        var result = await CreateHandler().Handle(new GetMyProfileSummaryQuery(), CancellationToken.None);

        result.PilotStartDate.Should().Be(LimaDay);
    }
}
