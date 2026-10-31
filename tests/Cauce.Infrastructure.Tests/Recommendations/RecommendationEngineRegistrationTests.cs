using Cauce.Application.Common.Interfaces.Recommendations;
using Cauce.Application.Recommendations.Contracts;
using Cauce.Infrastructure.Recommendations.Engines;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Cauce.Infrastructure.Tests.Recommendations;

/// <summary>
/// Pruebas de la selección del motor por configuración en la inyección de dependencias.
/// </summary>
public sealed class RecommendationEngineRegistrationTests
{
    private static ServiceProvider BuildProvider(params (string Key, string? Value)[] recommendations)
    {
        var settings = recommendations.ToDictionary(setting => $"Recommendations:{setting.Key}", setting => setting.Value);
        settings["ConnectionStrings:Cauce"] = "Host=localhost";

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new ServiceCollection().AddLogging().AddInfrastructure(configuration).BuildServiceProvider();
    }

    [Fact]
    public void Resolve_WithoutConfiguration_UsesTheRuleEngine()
    {
        using var provider = BuildProvider();

        provider.GetRequiredService<IRecommendationEngine>().Should().BeOfType<FodmapRuleRecommendationEngine>();
    }

    [Fact]
    public void Resolve_Xgboost_CreatesOneSharedEngine()
    {
        using var provider = BuildProvider(
            ("EngineKind", "Xgboost"),
            ("XgboostModelPath", XgboostTestData.ModelPath),
            ("XgboostModelSha256", XgboostTestData.ModelSha256));

        var engine = provider.GetRequiredService<IRecommendationEngine>();

        engine.Should().BeOfType<XgboostRecommendationEngine>();
        provider.GetRequiredService<IRecommendationEngine>().Should().BeSameAs(engine);
    }

    [Fact]
    public async Task Dispose_ContainerWithXgboost_ReleasesTheSession()
    {
        var provider = BuildProvider(
            ("EngineKind", "Xgboost"),
            ("XgboostModelPath", XgboostTestData.ModelPath),
            ("XgboostModelSha256", XgboostTestData.ModelSha256));
        var engine = provider.GetRequiredService<IRecommendationEngine>();
        var input = new RecommendationEngineInput(
            new PatientContextSnapshot(35, "Femenino", 24.5m, "SII-D", 3, "Media", false, false),
            [],
            new MealContext("almuerzo"));

        await provider.DisposeAsync();

        var act = () => engine.ScoreFoodsAsync(input, CancellationToken.None);
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public void Resolve_XgboostWithoutOptions_FailsValidationNamingBothOptions()
    {
        using var provider = BuildProvider(("EngineKind", "Xgboost"));

        var act = () => provider.GetRequiredService<IRecommendationEngine>();

        act.Should().Throw<OptionsValidationException>()
            .WithMessage("*XgboostModelPath*XgboostModelSha256*");
    }

    [Fact]
    public void Resolve_InvalidEngineKind_FailsValidation()
    {
        using var provider = BuildProvider(("EngineKind", "Forest"));

        var act = () => provider.GetRequiredService<IRecommendationEngine>();

        act.Should().Throw<OptionsValidationException>().WithMessage("*EngineKind*Forest*");
    }
}
