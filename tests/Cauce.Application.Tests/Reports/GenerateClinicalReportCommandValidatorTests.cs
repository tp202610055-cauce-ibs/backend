using Cauce.Application.Reports.UseCases.GenerateClinicalReport;
using FluentAssertions;

namespace Cauce.Application.Tests.Reports;

/// <summary>
/// Pruebas del período del reporte del nutricionista: sin tope de duración, inicio igual o anterior al fin
/// y fin no futuro.
/// </summary>
public sealed class GenerateClinicalReportCommandValidatorTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private static bool IsValid(DateOnly start, DateOnly end) =>
        new GenerateClinicalReportCommandValidator()
            .Validate(new GenerateClinicalReportCommand(Guid.NewGuid(), start, end, Guid.NewGuid()))
            .IsValid;

    [Theory]
    [InlineData(91)]
    [InlineData(120)]
    [InlineData(730)]
    public void Validate_PeriodLongerThan90Days_Succeeds(int days)
    {
        IsValid(Today.AddDays(-days), Today).Should().BeTrue();
    }

    [Fact]
    public void Validate_StartEqualToEnd_Succeeds()
    {
        IsValid(Today, Today).Should().BeTrue();
    }

    [Fact]
    public void Validate_StartAfterEnd_Fails()
    {
        IsValid(Today, Today.AddDays(-1)).Should().BeFalse();
    }

    [Fact]
    public void Validate_EndInTheFuture_Fails()
    {
        IsValid(Today.AddDays(-7), Today.AddDays(1)).Should().BeFalse();
    }
}
