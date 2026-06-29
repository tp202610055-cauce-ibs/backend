using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry.Enums;
using FluentAssertions;

namespace Cauce.Domain.Tests.ClinicalRegistry;

/// <summary>
/// Pruebas del helper de puntuación <see cref="IbsSssScoring"/>.
/// </summary>
public sealed class IbsSssScoringTests
{
    [Fact]
    public void CalculateTotal_SumsFiveDimensions()
    {
        IbsSssScoring.CalculateTotal(10, 20, 30, 40, 50).Should().Be(150);
    }

    [Theory]
    [InlineData(0, SeverityCategory.Mild)]
    [InlineData(174, SeverityCategory.Mild)]
    [InlineData(175, SeverityCategory.Moderate)]
    [InlineData(300, SeverityCategory.Moderate)]
    [InlineData(301, SeverityCategory.Severe)]
    [InlineData(500, SeverityCategory.Severe)]
    public void Categorize_AtBoundaries_ReturnsExpected(int total, SeverityCategory expected)
    {
        IbsSssScoring.Categorize(total).Should().Be(expected);
    }
}
