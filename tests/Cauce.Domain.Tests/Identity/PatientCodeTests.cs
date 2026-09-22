using Cauce.Domain.Identity;
using Cauce.Domain.Identity.Exceptions;
using FluentAssertions;

namespace Cauce.Domain.Tests.Identity;

/// <summary>
/// Pruebas del value object <see cref="PatientCode"/> (G1): formato canónico, rechazo de formatos
/// inválidos e igualdad estructural.
/// </summary>
public sealed class PatientCodeTests
{
    [Theory]
    [InlineData(1, "PAC-0001")]
    [InlineData(42, "PAC-0042")]
    [InlineData(999, "PAC-0999")]
    [InlineData(1000, "PAC-1000")]
    [InlineData(9999, "PAC-9999")]
    [InlineData(10000, "PAC-10000")]
    public void FromCorrelative_ValidCorrelative_RendersCanonicalFormat(long correlative, string expected)
    {
        PatientCode.FromCorrelative(correlative).Value.Should().Be(expected);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void FromCorrelative_NonPositiveCorrelative_Throws(long correlative)
    {
        var act = () => PatientCode.FromCorrelative(correlative);

        act.Should().Throw<InvalidPatientCodeException>();
    }

    [Theory]
    [InlineData("PAC-0001")]
    [InlineData("PAC-0042")]
    [InlineData("PAC-12345")]
    public void Parse_CanonicalValue_Succeeds(string value)
    {
        PatientCode.Parse(value).Value.Should().Be(value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("PAC-42")]            // menos de cuatro dígitos
    [InlineData("PAC-00042")]         // relleno no canónico del mismo correlativo
    [InlineData("PAC-0000")]          // el correlativo cero no existe
    [InlineData("pac-0042")]          // el prefijo es sensible a mayúsculas
    [InlineData("PAC0042")]           // falta el guion
    [InlineData("PAC-004A")]          // carácter no numérico
    [InlineData("PAC-0042 ")]         // espacio final
    [InlineData("PACIENTE-0042")]
    [InlineData("0042")]
    public void Parse_InvalidFormat_Throws(string? value)
    {
        var act = () => PatientCode.Parse(value);

        act.Should().Throw<InvalidPatientCodeException>();
    }

    [Theory]
    [InlineData("PAC-00042")]
    [InlineData("PAC-42")]
    public void TryParse_InvalidFormat_ReturnsFalseWithoutThrowing(string value)
    {
        PatientCode.TryParse(value, out var code).Should().BeFalse();
        code.Should().BeNull();
    }

    [Fact]
    public void Equality_SameCorrelative_IsStructural()
    {
        PatientCode.FromCorrelative(42).Should().Be(PatientCode.Parse("PAC-0042"));
        PatientCode.FromCorrelative(42).Should().NotBe(PatientCode.FromCorrelative(43));
    }
}
