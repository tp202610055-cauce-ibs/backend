using Cauce.Application.Common.Interfaces.Patients;
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
    // Vigencia de la URL prefirmada: 7 días, el máximo admitido por el esquema de firma S3/MinIO.
    private static readonly TimeSpan UrlValidity = TimeSpan.FromDays(7);

    private readonly CauceDbContext _context;
    private readonly IObjectStorage _objectStorage;
    private readonly MinioOptions _options;

    /// <summary>
    /// Inicializa el exportador con sus dependencias.
    /// </summary>
    /// <param name="context">Contexto de base de datos.</param>
    /// <param name="objectStorage">Almacenamiento de objetos.</param>
    /// <param name="options">Opciones de MinIO (bucket de exportaciones).</param>
    public ClinicalDataExporter(
        CauceDbContext context,
        IObjectStorage objectStorage,
        IOptions<MinioOptions> options)
    {
        _context = context;
        _objectStorage = objectStorage;
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task<ClinicalDataExport> ExportAsync(Guid patientUserId, CancellationToken ct = default)
    {
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
            .GetPresignedUrlAsync(_options.ExportsBucket, objectKey, UrlValidity, ct)
            .ConfigureAwait(false);

        return new ClinicalDataExport(url, DateTime.UtcNow + UrlValidity, archive.Counts);
    }

    private async Task<ClinicalDataSet> LoadAsync(Guid patientUserId, CancellationToken ct)
    {
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
            profile, allergies, meals, symptoms, assessments, recommendations, feedback, consentRecords, auditLogs);
    }
}
