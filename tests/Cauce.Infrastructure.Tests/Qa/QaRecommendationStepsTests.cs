using Cauce.Infrastructure.Qa.Manifest;
using FluentAssertions;

namespace Cauce.Infrastructure.Tests.Qa;

/// <summary>
/// Pasos de las recomendaciones del manifiesto: los del caso, los fijos de <c>stepsCount</c> y sus reglas.
/// </summary>
public sealed class QaRecommendationStepsTests
{
    private static QaRecommendationSeed Single(string recommendation) =>
        QaManifestLoader.Parse(QaTestManifests.Minimal(seeds: $"[{recommendation}]"))
            .Cases[0].Seeds.OfType<QaRecommendationSeed>().Single();

    private static IReadOnlyList<string> Errors(string recommendation)
    {
        var act = () => QaManifestLoader.Parse(QaTestManifests.Minimal(seeds: $"[{recommendation}]"));
        return act.Should().Throw<QaManifestException>().Which.Errors;
    }

    [Fact]
    public void SeededSteps_ExplicitSteps_AreTheCaseSteps()
    {
        var seed = Single("""{"t":"rec","patient":"paciente.qa@ejemplo.com","title":"T","status":"pendingReview","steps":["Uno","Dos"]}""");

        seed.SeededSteps.Should().Equal("Uno", "Dos");
    }

    [Fact]
    public void SeededSteps_OnlyStepsCount_TakesThatManyDefaultSteps()
    {
        var seed = Single("""{"t":"rec","patient":"paciente.qa@ejemplo.com","title":"T","status":"approvedAsGenerated","stepsCount":2}""");

        seed.SeededSteps.Should().Equal(QaRecommendationSeed.DefaultSteps.Take(2));
    }

    [Fact]
    public void SeededSteps_NeitherStepsNorCount_IsEmpty()
    {
        var seed = Single("""{"t":"rec","patient":"paciente.qa@ejemplo.com","title":"T","status":"approvedAsGenerated"}""");

        seed.SeededSteps.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void Parse_StepsCountOutOfRange_IsRejected(int count)
    {
        var errors = Errors($$"""{"t":"rec","patient":"paciente.qa@ejemplo.com","title":"T","status":"approvedAsGenerated","stepsCount":{{count}}}""");

        errors.Should().ContainSingle(error => error.Contains("«stepsCount» debe estar entre 1 y 3"));
    }

    [Fact]
    public void Parse_StepsCountDifferentFromSteps_IsRejected()
    {
        var errors = Errors("""{"t":"rec","patient":"paciente.qa@ejemplo.com","title":"T","status":"pendingReview","stepsCount":2,"steps":["Uno"]}""");

        errors.Should().ContainSingle(error => error.Contains("no coincide"));
    }

    [Fact]
    public void Parse_BlankStep_IsRejected()
    {
        var errors = Errors("""{"t":"rec","patient":"paciente.qa@ejemplo.com","title":"T","status":"pendingReview","steps":["Uno","  "]}""");

        errors.Should().ContainSingle(error => error.Contains("cada paso"));
    }

    [Fact]
    public void Parse_StepOverMaximumLength_IsRejected()
    {
        var errors = Errors($$"""{"t":"rec","patient":"paciente.qa@ejemplo.com","title":"T","status":"pendingReview","steps":["{{new string('a', 501)}}"]}""");

        errors.Should().ContainSingle(error => error.Contains("cada paso"));
    }

    [Fact]
    public void PublishedManifest_Cp042Cp077AndCp086_SeedTheirSteps()
    {
        var manifest = QaManifestLoader.LoadFile(QaTestManifests.PublishedPath());
        IReadOnlyList<string> StepsOf(string cp) => manifest.FindCase(cp)!.Seeds.OfType<QaRecommendationSeed>().Single().SeededSteps;

        StepsOf("CP042").Should().HaveCount(3);
        StepsOf("CP077").Should().HaveCount(3);
        StepsOf("CP086").Should().Equal("Cocinar la zanahoria al vapor");
    }
}
