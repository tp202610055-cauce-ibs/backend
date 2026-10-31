using Cauce.Application.Recommendations.Configuration;
using FluentAssertions;

namespace Cauce.Application.Tests.Recommendations;

/// <summary>
/// Pruebas de la validación del motor seleccionado y de las opciones del modelo XGBoost.
/// </summary>
public sealed class RecommendationsOptionsValidatorTests
{
    private const string ValidPath = @"C:\modelos\xgboost-sii-v1.2.0.onnx";
    private const string ValidHash = "eb9da0167ed7dc4f9db2d0f5e0da2b3e9eed0e250f993077c05fc378013f7481";

    private static Microsoft.Extensions.Options.ValidateOptionsResult Validate(RecommendationsOptions options) =>
        new RecommendationsOptionsValidator().Validate(null, options);

    private static RecommendationsOptions Xgboost(string path = ValidPath, string hash = ValidHash) =>
        new() { EngineKind = "Xgboost", XgboostModelPath = path, XgboostModelSha256 = hash };

    [Fact]
    public void Validate_DefaultOptions_SucceedsWithTheRuleEngine()
    {
        var options = new RecommendationsOptions();

        options.EngineKind.Should().Be("Rule");
        Validate(options).Succeeded.Should().BeTrue();
    }

    [Theory]
    [InlineData("Rule")]
    [InlineData("Onnx")]
    public void Validate_EngineThatDoesNotUseXgboost_DoesNotRequireItsOptions(string engineKind)
    {
        Validate(new RecommendationsOptions { EngineKind = engineKind }).Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_XgboostWithBothOptions_Succeeds()
    {
        Validate(Xgboost()).Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Validate_XgboostWithUpperCaseHash_Succeeds()
    {
        Validate(Xgboost(hash: ValidHash.ToUpperInvariant())).Succeeded.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("xgboost")]
    [InlineData("XGBOOST")]
    [InlineData("Forest")]
    public void Validate_UnknownEngineKind_Fails(string? engineKind)
    {
        var result = Validate(new RecommendationsOptions { EngineKind = engineKind! });

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("Recommendations:EngineKind");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_XgboostWithoutPath_Fails(string? path)
    {
        var result = Validate(Xgboost(path: path!));

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("Recommendations:XgboostModelPath");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_XgboostWithoutHash_Fails(string? hash)
    {
        var result = Validate(Xgboost(hash: hash!));

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("Recommendations:XgboostModelSha256");
    }

    [Theory]
    [InlineData("abc123")]
    [InlineData("eb9da0167ed7dc4f9db2d0f5e0da2b3e9eed0e250f993077c05fc378013f74810")]
    [InlineData("zb9da0167ed7dc4f9db2d0f5e0da2b3e9eed0e250f993077c05fc378013f7481")]
    public void Validate_XgboostWithMalformedHash_Fails(string hash)
    {
        var result = Validate(Xgboost(hash: hash));

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("64 caracteres hexadecimales");
    }
}
