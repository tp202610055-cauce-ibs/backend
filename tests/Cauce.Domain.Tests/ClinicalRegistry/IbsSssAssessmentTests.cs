using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry.Enums;
using Cauce.Domain.ClinicalRegistry.Exceptions;
using FluentAssertions;

namespace Cauce.Domain.Tests.ClinicalRegistry;

/// <summary>
/// Pruebas de invariantes y métodos de dominio de <see cref="IbsSssAssessment"/>.
/// </summary>
public sealed class IbsSssAssessmentTests
{
    private static readonly DateTime Now = new(2026, 6, 25, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid PatientId = Guid.NewGuid();

    private static IbsSssAssessment Submit(
        AssessmentType type, int cycle, int pain = 50, int freq = 50, int bloat = 50, int bowel = 50, int life = 50) =>
        IbsSssAssessment.Submit(Guid.NewGuid(), PatientId, type, cycle, pain, freq, bloat, bowel, life, Now);

    [Fact]
    public void Submit_Baseline_CycleZero_Succeeds()
    {
        var assessment = Submit(AssessmentType.Baseline, 0);

        assessment.AssessmentType.Should().Be(AssessmentType.Baseline);
        assessment.NextAssessmentDate.Should().Be(DateOnly.FromDateTime(Now).AddDays(14));
    }

    [Fact]
    public void Submit_Periodic_CycleOne_Succeeds()
    {
        Submit(AssessmentType.Periodic, 1).CycleNumber.Should().Be(1);
    }

    [Fact]
    public void Submit_BaselineWithNonZeroCycle_Throws()
    {
        var act = () => Submit(AssessmentType.Baseline, 1);

        act.Should().Throw<InvalidIbsSssDimensionException>();
    }

    [Fact]
    public void Submit_PeriodicWithZeroCycle_Throws()
    {
        var act = () => Submit(AssessmentType.Periodic, 0);

        act.Should().Throw<InvalidIbsSssDimensionException>();
    }

    [Fact]
    public void Submit_DimensionOutOfRange_Throws()
    {
        var act = () => Submit(AssessmentType.Baseline, 0, pain: 101);

        act.Should().Throw<InvalidIbsSssDimensionException>();
    }

    [Theory]
    [InlineData(0, 0, 0, 0, 0, 0, SeverityCategory.Mild)]
    [InlineData(50, 50, 50, 0, 0, 150, SeverityCategory.Mild)]
    [InlineData(50, 50, 50, 50, 0, 200, SeverityCategory.Moderate)]
    [InlineData(100, 100, 100, 100, 0, 400, SeverityCategory.Severe)]
    public void Submit_ComputesTotalAndCategory(int pain, int freq, int bloat, int bowel, int life, int expectedTotal, SeverityCategory expectedCategory)
    {
        var assessment = Submit(AssessmentType.Baseline, 0, pain, freq, bloat, bowel, life);

        assessment.TotalScore.Should().Be(expectedTotal);
        assessment.SeverityCategory.Should().Be(expectedCategory);
    }

    [Fact]
    public void IsClinicallySignificantImprovement_DeltaFifty_ReturnsTrue()
    {
        var baseline = Submit(AssessmentType.Baseline, 0, 100, 0, 0, 0, 0); // total 100
        var periodic = Submit(AssessmentType.Periodic, 1, 50, 0, 0, 0, 0); // total 50

        periodic.IsClinicallySignificantImprovement(baseline).Should().BeTrue();
    }

    [Fact]
    public void IsClinicallySignificantImprovement_DeltaFortyNine_ReturnsFalse()
    {
        var baseline = Submit(AssessmentType.Baseline, 0, 100, 0, 0, 0, 0); // total 100
        var periodic = Submit(AssessmentType.Periodic, 1, 51, 0, 0, 0, 0); // total 51

        periodic.IsClinicallySignificantImprovement(baseline).Should().BeFalse();
    }

    [Fact]
    public void CompareTotalScoreTo_Improvement_ReturnsNegative()
    {
        var baseline = Submit(AssessmentType.Baseline, 0, 80, 0, 0, 0, 0); // 80
        var periodic = Submit(AssessmentType.Periodic, 1, 30, 0, 0, 0, 0); // 30

        periodic.CompareTotalScoreTo(baseline).Should().Be(-50);
    }
}
