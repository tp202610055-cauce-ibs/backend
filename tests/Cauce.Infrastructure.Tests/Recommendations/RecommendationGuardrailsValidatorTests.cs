using Cauce.Application.Recommendations.Configuration;
using Cauce.Infrastructure.Recommendations.Llm;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Cauce.Infrastructure.Tests.Recommendations;

/// <summary>
/// Pruebas de los guardrails clínicos sobre la explicación generada por el LLM (DEC-B4-07).
/// </summary>
public sealed class RecommendationGuardrailsValidatorTests
{
    private static readonly RecommendationGuardrailsValidator Validator =
        new(Options.Create(new RecommendationsOptions()));

    [Fact]
    public void Validate_CleanSpanishText_Ok()
    {
        const string text = "Se sugiere reducir el consumo de algunos alimentos para el bienestar del paciente con criterio profesional.";

        Validator.Validate(text).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_TooShort_Fails()
    {
        var result = Validator.Validate("Muy corto.");

        result.IsValid.Should().BeFalse();
        result.FailureReason.Should().Be("too_short");
    }

    [Fact]
    public void Validate_ContainsClinicalTerm_Fails()
    {
        const string text = "El paciente tiene un diagnóstico de la enfermedad y debe seguir el tratamiento indicado por su médico.";

        Validator.Validate(text).FailureReason.Should().Be("contains_clinical_term");
    }

    [Fact]
    public void Validate_ContainsCertaintyTerm_Fails()
    {
        const string text = "Este alimento siempre te hará bien y sin duda mejorará tu bienestar de manera notable para toda la familia.";

        Validator.Validate(text).FailureReason.Should().Be("contains_certainty_term");
    }

    [Fact]
    public void Validate_ContainsCausalClaim_Fails()
    {
        const string text = "Este alimento te causa molestias y por eso conviene evitarlo dentro de tu alimentación diaria habitual.";

        Validator.Validate(text).FailureReason.Should().Be("contains_causal_claim");
    }

    [Fact]
    public void Validate_NonSpanishText_Fails()
    {
        const string text = "This dietary recommendation about food and wellbeing was written using only english words right now here.";

        Validator.Validate(text).FailureReason.Should().Be("not_spanish");
    }
}
