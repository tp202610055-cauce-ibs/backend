using Cauce.Application.Common.Interfaces.Patients;
using Cauce.Domain.Identity.Exceptions;
using Cauce.Application.Common.Interfaces.Storage;
using Cauce.Infrastructure.Persistence;
using Cauce.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Cauce.Infrastructure.Patients;

/// <summary>
/// Implementación de <see cref="IClinicalDataExporter"/> que materializa los datos del paciente desde
/// la base de datos, los empaqueta en un ZIP de CSVs con <see cref="ClinicalDataArchiveBuilder"/>, lo
/// sube a MinIO y devuelve una URL de descarga prefirmada (US25).
/// </summary>
public sealed class ClinicalDataExporter : IClinicalDataExporter
{
    private readonly CauceDbContext _context;
    private readonly IObjectStorage _objectStorage;
    private readonly MinioOptions _options;
    private readonly DataExportOptions _exportOptions;

    /// <summary>
    /// Inicializa el exportador con sus dependencias.
    /// </summary>
    /// <param name="context">Contexto de base de datos.</param>
    /// <param name="objectStorage">Almacenamiento de objetos.</param>
    /// <param name="options">Opciones de MinIO (bucket de exportaciones).</param>
    /// <param name="exportOptions">Opciones de la exportación (vigencia de la URL prefirmada).</param>
    public ClinicalDataExporter(
        CauceDbContext context,
        IObjectStorage objectStorage,
        IOptions<MinioOptions> options,
        IOptions<DataExportOptions> exportOptions)
    {
        _context = context;
        _objectStorage = objectStorage;
        _options = options.Value;
        _exportOptions = exportOptions.Value;
    }

    /// <inheritdoc />
    public async Task<ClinicalDataExport> ExportAsync(Guid patientUserId, CancellationToken ct = default)
    {
        var urlValidity = TimeSpan.FromMinutes(_exportOptions.PresignedUrlValidityMinutes);
        var dataSet = await LoadAsync(patientUserId, ct).ConfigureAwait(false);
        var archive = ClinicalDataArchiveBuilder.Build(dataSet);

        await _objectStorage.EnsureBucketExistsAsync(_options.ExportsBucket, ct).ConfigureAwait(false);

        var objectKey = $"{patientUserId}/{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}.zip";
        using (var content = new MemoryStream(archive.ZipContent, writable: false))
        {
            await _objectStorage
                .UploadAsync(_options.ExportsBucket, objectKey, content, "application/zip", ct)
                .ConfigureAwait(false);
        }

        var url = await _objectStorage
            .GetPresignedUrlAsync(_options.ExportsBucket, objectKey, urlValidity, ct)
            .ConfigureAwait(false);

        return new ClinicalDataExport(url, DateTime.UtcNow + urlValidity, archive.Counts);
    }

    private async Task<ClinicalDataSet> LoadAsync(Guid patientUserId, CancellationToken ct)
    {
        // El paciente viaja en el ZIP por su código legible (G1), nunca por su GUID ni por su nombre:
        // el archivo está pensado para investigación y un identificador técnico ahí no aporta nada y
        // sí reidentifica.
        var patientCode = await _context.Users
            .AsNoTracking()
            .Where(user => user.Id == patientUserId)
            .Select(user => user.PatientCode)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false)
            ?? throw new InvalidPatientCodeException(null);

        var profile = await _context.PatientProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == patientUserId, ct)
            .ConfigureAwait(false);

        var allergies = await _context.PatientAllergies
            .AsNoTracking()
            .Where(a => a.PatientId == patientUserId)
            .OrderBy(a => a.DeclaredAt)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var meals = await _context.Meals
            .AsNoTracking()
            .Include(m => m.Items)
            .Where(m => m.PatientId == patientUserId)
            .OrderBy(m => m.ConsumedAt)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var symptoms = await _context.Symptoms
            .AsNoTracking()
            .Where(s => s.PatientId == patientUserId)
            .OrderBy(s => s.OccurredAt)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var assessments = await _context.IbsSssAssessments
            .AsNoTracking()
            .Where(a => a.PatientId == patientUserId)
            .OrderBy(a => a.CompletedAt)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var recommendations = await _context.Recommendations
            .AsNoTracking()
            .Where(r => r.PatientId == patientUserId)
            .OrderBy(r => r.GeneratedAt)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var recommendationIds = recommendations.Select(r => r.Id).ToList();
        var feedback = await _context.RecommendationFeedback
            .AsNoTracking()
            .Where(f => recommendationIds.Contains(f.RecommendationId))
            .OrderBy(f => f.SubmittedAt)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var consentRecords = await _context.ConsentRecords
            .AsNoTracking()
            .Where(c => c.UserId == patientUserId)
            .OrderBy(c => c.AcceptedAt)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var auditLogs = await _context.AuditLogs
            .AsNoTracking()
            .Where(l => l.ActorUserId == patientUserId)
            .OrderBy(l => l.OccurredAt)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return new ClinicalDataSet(
            patientCode, profile, allergies, meals, symptoms, assessments, recommendations, feedback, consentRecords, auditLogs);
    }
}
