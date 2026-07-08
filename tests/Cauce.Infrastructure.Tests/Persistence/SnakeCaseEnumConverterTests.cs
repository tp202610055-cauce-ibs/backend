using Cauce.Domain.ClinicalRegistry.Enums;
using Cauce.Infrastructure.Persistence.Converters;
using FluentAssertions;

namespace Cauce.Infrastructure.Tests.Persistence;

/// <summary>
/// Pruebas del conversor de enums a snake_case, con foco en los nuevos tipos de síntoma del catálogo
/// de nueve (US11 CA01): <c>nausea</c>, <c>reflux</c>, <c>urgency</c>.
/// </summary>
public sealed class SnakeCaseEnumConverterTests
{
    private readonly SnakeCaseEnumConverter<SymptomType> _converter = new();

    [Theory]
    [InlineData(SymptomType.Nausea, "nausea")]
    [InlineData(SymptomType.Reflux, "reflux")]
    [InlineData(SymptomType.Urgency, "urgency")]
    [InlineData(SymptomType.AbdominalPain, "abdominal_pain")]
    public void ConvertToProvider_SerializesToSnakeCase(SymptomType value, string expected)
    {
        _converter.ConvertToProvider(value).Should().Be(expected);
    }

    [Theory]
    [InlineData("nausea", SymptomType.Nausea)]
    [InlineData("reflux", SymptomType.Reflux)]
    [InlineData("urgency", SymptomType.Urgency)]
    public void ConvertFromProvider_ParsesSnakeCase(string stored, SymptomType expected)
    {
        _converter.ConvertFromProvider(stored).Should().Be(expected);
    }
}
