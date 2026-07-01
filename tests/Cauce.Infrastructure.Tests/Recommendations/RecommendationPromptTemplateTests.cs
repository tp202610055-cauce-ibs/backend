using Cauce.Application.Recommendations.Contracts;
using Cauce.Domain.Recommendations.Enums;
using Cauce.Infrastructure.Recommendations.Llm;
using FluentAssertions;

namespace Cauce.Infrastructure.Tests.Recommendations;

/// <summary>
/// Pruebas de la construcción del prompt a Ollama, en particular el formato del razonamiento por
/// ítem (aclaración técnica del Bloque 4).
/// </summary>
public sealed class RecommendationPromptTemplateTests
{
    private static readonly PatientContextSnapshot Patient =
        new(30, "Masculino", 22m, "SII-M", 1, "Media", false, false);

    [Fact]
    public void Build_RendersActionTypeInSpanish()
    {
        var prompt = RecommendationPromptTemplate.Build(Patient, new[]
        {
            new ExplanationItem("Manzana", ActionType.Avoid, "razón breve")
        });

        prompt.Should().Contain("- Manzana (evitar): razón breve");
    }

    [Fact]
    public void Build_OmitsReasoningWhenNullOrEmpty()
    {
        var prompt = RecommendationPromptTemplate.Build(Patient, new[]
        {
            new ExplanationItem("Pera", ActionType.Reduce, null)
        });

        prompt.Should().Contain("- Pera (reducir)");
        prompt.Should().NotContain("- Pera (reducir):");
    }

    [Fact]
    public void Build_TruncatesReasoningAtOneHundredTwenty()
    {
        var longReasoning = new string('a', 130);

        var prompt = RecommendationPromptTemplate.Build(Patient, new[]
        {
            new ExplanationItem("Yogur", ActionType.Substitute, longReasoning)
        });

        prompt.Should().Contain(new string('a', 117) + "...");
        prompt.Should().NotContain(new string('a', 118));
    }
}
