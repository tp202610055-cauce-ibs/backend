using Cauce.Domain.Reports;

namespace Cauce.Application.Common.Interfaces.Reports;

/// <summary>
/// Repositorio del agregado <see cref="ClinicalReportMetadata"/>.
/// </summary>
public interface IClinicalReportMetadataRepository
{
    /// <summary>
    /// Agrega los metadatos de un reporte al contexto de persistencia (sin <c>SaveChanges</c>).
    /// </summary>
    /// <param name="metadata">Metadatos a agregar.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Tarea que representa la operación asíncrona.</returns>
    Task AddAsync(ClinicalReportMetadata metadata, CancellationToken ct = default);
}
