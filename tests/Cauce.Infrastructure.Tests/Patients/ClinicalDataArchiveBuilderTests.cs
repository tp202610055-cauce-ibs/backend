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
    private const string PatientCodeValue = "PAC-0042";

    private static readonly string[] ExpectedCsvNames = [.. ArchiveEntryNames.All];

    [Fact]
    public void Build_EmptyDataSet_AllCsvsPresentWithHeadersOnly()
    {
        var data = new ClinicalDataSet(
            PatientCode: PatientCodeValue,
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
            PatientCodeValue, profile, [], [], [symptom], [], [], [], [consent], []);

        var archive = ClinicalDataArchiveBuilder.Build(data);
        var entries = ReadEntries(archive.ZipContent);

        archive.Counts[ArchiveEntryNames.Symptoms].Should().Be(1);
        archive.Counts[ArchiveEntryNames.ConsentRecords].Should().Be(1);
        archive.Counts[ArchiveEntryNames.Profile].Should().Be(1);

        var symptomLines = entries[ArchiveEntryNames.Symptoms].Split('\n', StringSplitOptions.RemoveEmptyEntries);
        symptomLines.Should().HaveCount(2);
        symptomLines[0].Should().StartWith("symptom_id,");
        symptomLines[1].Should().Contain("Bloating");
    }

    [Fact]
    public void Build_EntryNames_AreFixedSpanishNamesWithoutAccents()
    {
        var archive = ClinicalDataArchiveBuilder.Build(EmptyDataSet());
        var entries = ReadEntries(archive.ZipContent);

        // G3: los nombres son parte del contrato del ZIP, no cadenas de presentación. Se comprueban
        // literalmente para que un cambio accidental rompa aquí y no en el equipo que abre el archivo.
        entries.Keys.Should().BeEquivalentTo(
            "perfil_clinico.csv", "alergias.csv", "comidas.csv", "sintomas.csv", "ibs_sss.csv",
            "recomendaciones.csv", "retroalimentacion.csv", "consentimientos.csv", "auditoria.csv");

        entries.Keys.Should().OnlyContain(name => name.All(c => c < 128),
            "los nombres viajan en una entrada ZIP y no deben depender del juego de caracteres del descompresor");
    }

    [Fact]
    public void Build_ProfileCsv_IdentifiesThePatientByCodeAndNotByTechnicalId()
    {
        var patientId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var profile = PatientProfile.Create(
            Guid.NewGuid(), patientId, DateOnly.FromDateTime(now).AddYears(-30), BiologicalSex.Female,
            60m, 165m, IbsSubtype.IbsD, null, null, now);

        var archive = ClinicalDataArchiveBuilder.Build(
            EmptyDataSet() with { Profile = profile });
        var entries = ReadEntries(archive.ZipContent);

        var lines = entries[ArchiveEntryNames.Profile].Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines[0].Should().StartWith("patient_code,");
        lines[1].Should().StartWith(PatientCodeValue + ",");

        // G1: el identificador técnico no viaja al archivo de investigación.
        entries[ArchiveEntryNames.Profile].Should().NotContain(patientId.ToString());
    }

    [Fact]
    public void Build_NoEntry_ContainsThePatientRealName()
    {
        const string realName = "Rosa Guadalupe Cordova";
        var patientId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var profile = PatientProfile.Create(
            Guid.NewGuid(), patientId, DateOnly.FromDateTime(now).AddYears(-30), BiologicalSex.Female,
            60m, 165m, IbsSubtype.IbsD, null, null, now);
        var symptom = Symptom.Report(
            Guid.NewGuid(), Guid.NewGuid(), patientId, SymptomType.Bloating, 40, now, now, now);
        var consent = ConsentRecord.Capture(
            Guid.NewGuid(), patientId, "1.0", new string('a', 64), "127.0.0.1", now);

        var archive = ClinicalDataArchiveBuilder.Build(
            EmptyDataSet() with { Profile = profile, Symptoms = [symptom], ConsentRecords = [consent] });
        var entries = ReadEntries(archive.ZipContent);

        // G1: el código seudonimiza. Si alguna vez el nombre real llegara a una columna del export,
        // el código dejaría de proteger nada porque ambos estarían en la misma salida.
        foreach (var (name, content) in entries)
        {
            content.Should().NotContain(realName, $"{name} no debe llevar el nombre del paciente");
        }

        entries[ArchiveEntryNames.Profile].Should().Contain(PatientCodeValue);
    }

    private static ClinicalDataSet EmptyDataSet() => new(
        PatientCode: PatientCodeValue,
        Profile: null,
        Allergies: [],
        Meals: [],
        Symptoms: [],
        Assessments: [],
        Recommendations: [],
        Feedback: [],
        ConsentRecords: [],
        AuditLogs: []);

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
