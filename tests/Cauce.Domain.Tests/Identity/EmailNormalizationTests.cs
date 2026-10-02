using System.Globalization;
using Cauce.Domain.Identity;
using FluentAssertions;

namespace Cauce.Domain.Tests.Identity;

/// <summary>
/// Pruebas de <see cref="EmailNormalization"/>, el criterio único del correo (acta A70).
/// </summary>
public sealed class EmailNormalizationTests
{
    [Theory]
    [InlineData("Ana.Perez@Cauce.Local", "ana.perez@cauce.local")]
    [InlineData("  ana@cauce.local ", "ana@cauce.local")]
    [InlineData("\tANA@CAUCE.LOCAL\n", "ana@cauce.local")]
    [InlineData("ana@cauce.local", "ana@cauce.local")]
    public void Normalize_TrimsAndLowercases(string raw, string expected)
    {
        EmailNormalization.Normalize(raw).Should().Be(expected);
    }

    [Fact]
    public void Normalize_Null_ReturnsEmpty()
    {
        EmailNormalization.Normalize(null).Should().BeEmpty();
    }

    [Fact]
    public void Normalize_DoesNotDependOnTheServerCulture()
    {
        // Con la cultura turca, ToLower() convierte "I" en "ı" (sin punto) y el correo dejaría de coincidir
        // con el que guarda Keycloak.
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
        try
        {
            EmailNormalization.Normalize("INFO@CAUCE.LOCAL").Should().Be("info@cauce.local");
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
