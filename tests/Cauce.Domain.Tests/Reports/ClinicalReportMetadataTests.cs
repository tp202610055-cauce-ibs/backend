using Cauce.Domain.Reports;
using Cauce.Domain.Reports.Exceptions;
using FluentAssertions;

namespace Cauce.Domain.Tests.Reports;

/// <summary>
/// Pruebas de la entidad <see cref="ClinicalReportMetadata"/>.
/// </summary>
public sealed class ClinicalReportMetadataTests
{
    private static readonly DateTime Now = new(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Register_ValidPeriod_CreatesMetadata()
    {
        var metadata = ClinicalReportMetadata.Register(
            Guid.NewGuid(), Guid.NewGuid(),
            new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 30),
            "patient/report.pdf", 2048, Now);

        metadata.ObjectStoragePath.Should().Be("patient/report.pdf");
        metadata.FileSizeBytes.Should().Be(2048);
        metadata.GeneratedAt.Should().Be(Now);
    }

    [Fact]
    public void Register_EndBeforeStart_Throws()
    {
        var act = () => ClinicalReportMetadata.Register(
            Guid.NewGuid(), Guid.NewGuid(),
            new DateOnly(2026, 6, 30), new DateOnly(2026, 6, 1),
            "patient/report.pdf", 2048, Now);

        act.Should().Throw<ReportPeriodInvalidException>();
    }
}
