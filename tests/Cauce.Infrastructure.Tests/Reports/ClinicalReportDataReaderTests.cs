using Cauce.Infrastructure.Reports;
using FluentAssertions;

namespace Cauce.Infrastructure.Tests.Reports;

/// <summary>
/// Pruebas del formato de iniciales del reporte clínico (CA-B5-28): el paciente se identifica por sus
/// iniciales, nunca por su nombre completo.
/// </summary>
public sealed class ClinicalReportDataReaderTests
{
    [Theory]
    [InlineData("Rosa García Cruz", "R. G. C.")]
    [InlineData("Juan Pérez", "J. P.")]
    [InlineData("Ana", "A.")]
    public void BuildInitials_ProducesInitialsPattern(string fullName, string expected)
    {
        ClinicalReportDataReader.BuildInitials(fullName).Should().Be(expected);
    }

    [Fact]
    public void BuildInitials_BlankName_ReturnsPlaceholder()
    {
        ClinicalReportDataReader.BuildInitials("   ").Should().Be("—");
    }
}
