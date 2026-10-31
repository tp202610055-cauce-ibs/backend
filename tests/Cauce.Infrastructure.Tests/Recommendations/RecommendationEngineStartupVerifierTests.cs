using Cauce.Application.Common.Interfaces.Recommendations;
using Cauce.Application.Recommendations.Configuration;
using Cauce.Application.Recommendations.Contracts;
using Cauce.Domain.Recommendations;
using Cauce.Infrastructure.Recommendations;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Cauce.Infrastructure.Tests.Recommendations;

/// <summary>
/// Pruebas de la comprobación de arranque: solo actúa con Xgboost y exige que la versión activa de la base
/// coincida en nombre y hash con el motor configurado.
/// </summary>
public sealed class RecommendationEngineStartupVerifierTests
{
    private const string VersionName = "xgboost-sii-v1.2.0";
    private const string ModelHash = "abc123";

    private readonly IModelVersionRepository _repository = Substitute.For<IModelVersionRepository>();
    private readonly IRecommendationEngine _engine = Substitute.For<IRecommendationEngine>();

    /// <summary>
    /// Configura el motor doble con el descriptor del modelo real.
    /// </summary>
    public RecommendationEngineStartupVerifierTests()
    {
        _engine.Descriptor.Returns(new ModelVersionDescriptor(VersionName, ModelHash, "Xgboost"));
    }

    private RecommendationEngineStartupVerifier Verifier(string engineKind)
    {
        var provider = new ServiceCollection()
            .AddScoped(_ => _repository)
            .AddSingleton(_ => _engine)
            .BuildServiceProvider();

        return new RecommendationEngineStartupVerifier(
            provider,
            Options.Create(new RecommendationsOptions { EngineKind = engineKind }),
            NullLogger<RecommendationEngineStartupVerifier>.Instance);
    }

    private void ActiveVersionIs(ModelVersion? version)
    {
        _repository.GetActiveAsync(Arg.Any<CancellationToken>()).Returns(version);
    }

    private static ModelVersion Version(string name, string hash) =>
        ModelVersion.Register(name, hash, null, "{}", "system-seeder", DateTime.UtcNow);

    [Theory]
    [InlineData("Rule")]
    [InlineData("Onnx")]
    public async Task StartAsync_OtherEngines_TouchNeitherTheEngineNorTheDatabase(string engineKind)
    {
        await Verifier(engineKind).StartAsync(CancellationToken.None);

        _ = _engine.DidNotReceive().Descriptor;
        await _repository.DidNotReceive().GetActiveAsync(Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("abc123")]
    [InlineData("ABC123")]
    public async Task StartAsync_ActiveVersionMatchesEngine_Completes(string activeHash)
    {
        ActiveVersionIs(Version(VersionName, activeHash));

        var act = () => Verifier("Xgboost").StartAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task StartAsync_NoActiveVersion_Throws()
    {
        ActiveVersionIs(null);

        var act = () => Verifier("Xgboost").StartAsync(CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*ninguna*xgboost-sii-v1.2.0*seed-catalogs*");
    }

    [Fact]
    public async Task StartAsync_ActiveVersionWithAnotherName_Throws()
    {
        ActiveVersionIs(Version("rule-v1.0.0", ModelHash));

        var act = () => Verifier("Xgboost").StartAsync(CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*rule-v1.0.0*xgboost-sii-v1.2.0*");
    }

    [Fact]
    public async Task StartAsync_ActiveVersionWithAnotherHash_Throws()
    {
        ActiveVersionIs(Version(VersionName, "otro-hash"));

        var act = () => Verifier("Xgboost").StartAsync(CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*SHA-256*");
    }
}
