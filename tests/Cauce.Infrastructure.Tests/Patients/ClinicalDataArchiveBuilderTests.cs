using System.IO.Compression;
using System.Text;
using Cauce.Domain.Auditing;
using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry.Enums;
using Cauce.Domain.Identity;
using Cauce.Domain.Patients;
using Cauce.Domain.Patients.Enums;
using Cauce.Domain.Recommendations;
using Cauce.Infrastructure.Patients;
using FluentAssertions;

namespace Cauce.Infrastructure.Tests.Patients;

/// <summary>
/// Pruebas del constructor del archivo de exportación de datos del paciente (US25). Verifican que
/// todos los CSV estén presentes con sus encabezados aunque no haya filas (CA02) y que las filas se
/// serialicen correctamente.
/// </summary>
public sealed class ClinicalDataArchiveBuilderTests
{
    private static readonly string[] ExpectedCsvNames =
    [
        "profile.csv", "allergies.csv", "meals.csv", "symptoms.csv", "ibs_sss_assessments.csv",
        "recommendations.csv", "recommendation_feedback.csv", "consent_records.csv", "audit_logs.csv"
    ];

    [Fact]
    public void Build_EmptyDataSet_AllCsvsPresentWithHeadersOnly()
    {
        var data = new ClinicalDataSet(
            Profile: null,
            Allergies: [],
            Meals: [],
            Symptoms: [],
            Assessments: [],
            Recommendations: [],
            Feedback: [],
            ConsentRecords: [],
            AuditLogs: []);

        var archive = ClinicalDataArchiveBuilder.Build(data);

        var entries = ReadEntries(archive.ZipContent);
        entries.Keys.Should().BeEquivalentTo(ExpectedCsvNames);

        // CA02: cada CSV tiene su encabezado aunque no haya filas de datos.
        foreach (var (name, content) in entries)
        {
            var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            lines.Should().ContainSingle($"{name} debe tener solo el encabezado cuando no hay datos.");
            lines[0].Should().NotBeNullOrWhiteSpace();
        }

        archive.Counts.Values.Should().OnlyContain(count => count == 0);
    }

    [Fact]
    public void Build_WithSymptomAndConsent_SerializesRowsAndCounts()
    {
        var patientId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var symptom = Symptom.Report(
            Guid.NewGuid(), Guid.NewGuid(), patientId, SymptomType.Bloating, 40, now, now, now);
        var consent = ConsentRecord.Capture(Guid.NewGuid(), patientId, "1.0", new string('a', 64), "127.0.0.1", now);
        var profile = PatientProfile.Create(
            Guid.NewGuid(), patientId, DateOnly.FromDateTime(now).AddYears(-30), BiologicalSex.Female,
            60m, 165m, IbsSubtype.IbsD, null, null, now);

        var data = new ClinicalDataSet(
            profile, [], [], [symptom], [], [], [], [consent], []);

        var archive = ClinicalDataArchiveBuilder.Build(data);
        var entries = ReadEntries(archive.ZipContent);

        archive.Counts["symptoms.csv"].Should().Be(1);
        archive.Counts["consent_records.csv"].Should().Be(1);
        archive.Counts["profile.csv"].Should().Be(1);

        var symptomLines = entries["symptoms.csv"].Split('\n', StringSplitOptions.RemoveEmptyEntries);
        symptomLines.Should().HaveCount(2);
        symptomLines[0].Should().StartWith("symptom_id,");
        symptomLines[1].Should().Contain("Bloating");
    }

    private static Dictionary<string, string> ReadEntries(byte[] zipBytes)
    {
        using var stream = new MemoryStream(zipBytes);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in archive.Entries)
        {
            using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
            result[entry.Name] = reader.ReadToEnd();
        }

        return result;
    }
}
