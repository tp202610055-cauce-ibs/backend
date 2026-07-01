using Cauce.Application.Recommendations.Contracts;
using Cauce.Domain.Recommendations.Enums;
using Cauce.Infrastructure.Recommendations.Llm;
using FluentAssertions;

namespace Cauce.Infrastructure.Tests.Recommendations;

/// <summary>
/// Pruebas del proveedor de explicaciones de respaldo (DEC-B4-07).
/// </summary>
public sealed class FallbackExplanationProviderTests
{
    private readonly FallbackExplanationProvider _provider = new();

    [Fact]
    public void GenerateFor_ReturnsFallbackSource()
    {
        var result = _provider.GenerateFor(new[] { new ExplanationItem("Manzana", ActionType.Avoid, null) });

        result.Source.Should().Be(ExplanationSource.Fallback);
    }

    [Fact]
    public void GenerateFor_TextMeetsMinimumLength()
    {
        var result = _provider.GenerateFor(new[]
        {
            new ExplanationItem("Manzana", ActionType.Avoid, null),
            new ExplanationItem("Pera", ActionType.Reduce, null)
        });

        result.Text.Length.Should().BeGreaterThanOrEqualTo(80);
    }
}
