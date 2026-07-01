using Cauce.Domain.Recommendations.Exceptions;
using Cauce.Domain.Recommendations.ValueObjects;
using FluentAssertions;

namespace Cauce.Domain.Tests.Recommendations;

/// <summary>
/// Pruebas del value object <see cref="ConfidenceScore"/>.
/// </summary>
public sealed class ConfidenceScoreTests
{
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    [InlineData(0.123)]
    public void Create_WithValidValue_Succeeds(decimal value)
    {
        ConfidenceScore.Create(value).Value.Should().Be(value);
    }

    [Fact]
    public void Create_RoundsToThreeDecimals()
    {
        ConfidenceScore.Create(0.12349m).Value.Should().Be(0.123m);
    }

    [Fact]
    public void Create_BelowZero_Throws()
    {
        var act = () => ConfidenceScore.Create(-0.01m);

        act.Should().Throw<InvalidConfidenceScoreException>();
    }

    [Fact]
    public void Create_AboveOne_Throws()
    {
        var act = () => ConfidenceScore.Create(1.01m);

        act.Should().Throw<InvalidConfidenceScoreException>();
    }

    [Fact]
    public void Equality_BasedOnValue()
    {
        ConfidenceScore.Create(0.5m).Should().Be(ConfidenceScore.Create(0.5m));
        ConfidenceScore.Create(0.5m).Should().NotBe(ConfidenceScore.Create(0.6m));
    }
}
