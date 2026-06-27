using Cauce.Domain.Patients;
using Cauce.Domain.Patients.Enums;
using Cauce.Domain.Patients.Exceptions;
using FluentAssertions;

namespace Cauce.Domain.Tests.Patients;

/// <summary>
/// Pruebas de invariantes y métodos de dominio de <see cref="PatientProfile"/>.
/// </summary>
public sealed class PatientProfileTests
{
    private static readonly DateTime Now = new(2026, 6, 25, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Guid UserId = Guid.NewGuid();

    private static PatientProfile CreateValid(decimal weightKg = 70m, decimal heightCm = 175m)
    {
        var dob = DateOnly.FromDateTime(Now).AddYears(-30);
        return PatientProfile.Create(Guid.NewGuid(), UserId, dob, BiologicalSex.Male, weightKg, heightCm, IbsSubtype.IbsM, null, null, Now);
    }

    [Fact]
    public void Create_ValidValues_CreatesIncompleteOnboardingProfile()
    {
        var profile = CreateValid();

        profile.OnboardingCompleted.Should().BeFalse();
        profile.UserId.Should().Be(UserId);
    }

    [Fact]
    public void Create_AgeBelow18_Throws()
    {
        var dob = DateOnly.FromDateTime(Now).AddYears(-17);
        var act = () => PatientProfile.Create(Guid.NewGuid(), UserId, dob, BiologicalSex.Female, 60m, 165m, IbsSubtype.IbsC, null, null, Now);

        act.Should().Throw<InvalidBiometricValueException>();
    }

    [Fact]
    public void Create_AgeAbove120_Throws()
    {
        var dob = DateOnly.FromDateTime(Now).AddYears(-121);
        var act = () => PatientProfile.Create(Guid.NewGuid(), UserId, dob, BiologicalSex.Female, 60m, 165m, IbsSubtype.IbsC, null, null, Now);

        act.Should().Throw<InvalidBiometricValueException>();
    }

    [Fact]
    public void Create_WeightOutOfRange_Throws()
    {
        var act = () => CreateValid(weightKg: 600m);

        act.Should().Throw<InvalidBiometricValueException>();
    }

    [Fact]
    public void Create_HeightOutOfRange_Throws()
    {
        var act = () => CreateValid(heightCm: 300m);

        act.Should().Throw<InvalidBiometricValueException>();
    }

    [Fact]
    public void Create_FutureDiagnosisDate_Throws()
    {
        var dob = DateOnly.FromDateTime(Now).AddYears(-30);
        var futureDiagnosis = DateOnly.FromDateTime(Now).AddDays(1);
        var act = () => PatientProfile.Create(Guid.NewGuid(), UserId, dob, BiologicalSex.Male, 70m, 175m, IbsSubtype.IbsM, futureDiagnosis, null, Now);

        act.Should().Throw<InvalidBiometricValueException>();
    }

    [Fact]
    public void CalculateBmi_KnownValues_Returns22Point86()
    {
        var profile = CreateValid(weightKg: 70m, heightCm: 175m);

        profile.CalculateBmi().Should().Be(22.86m);
    }

    [Theory]
    [InlineData(50, 175, "underweight")]
    [InlineData(70, 175, "normal")]
    [InlineData(80, 175, "overweight")]
    [InlineData(100, 175, "obese")]
    public void GetBmiCategory_ForRanges_ReturnsExpected(double weight, double height, string expected)
    {
        var profile = CreateValid((decimal)weight, (decimal)height);

        profile.GetBmiCategory().Should().Be(expected);
    }

    [Fact]
    public void UpdateBiometrics_UpdatesValuesAndTimestamp()
    {
        var profile = CreateValid();
        var later = Now.AddHours(1);

        profile.UpdateBiometrics(80m, 180m, later);

        profile.WeightKg.Should().Be(80m);
        profile.HeightCm.Should().Be(180m);
        profile.UpdatedAt.Should().Be(later);
    }

    [Fact]
    public void CompleteOnboarding_IsIdempotent()
    {
        var profile = CreateValid();
        profile.CompleteOnboarding(Now.AddHours(1));
        var firstUpdate = profile.UpdatedAt;

        profile.CompleteOnboarding(Now.AddHours(2));

        profile.OnboardingCompleted.Should().BeTrue();
        profile.UpdatedAt.Should().Be(firstUpdate);
    }
}
