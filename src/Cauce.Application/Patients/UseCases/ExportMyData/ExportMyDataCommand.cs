using MediatR;

namespace Cauce.Application.Patients.UseCases.ExportMyData;

/// <summary>
/// Comando para exportar todos los datos personales y clínicos del paciente autenticado en ejercicio
/// del derecho a la portabilidad de datos (US25, Ley N° 29733).
/// </summary>
public sealed record ExportMyDataCommand : IRequest<ExportMyDataResult>;

/// <summary>
/// Resultado de la exportación de datos del paciente.
/// </summary>
/// <param name="DownloadUrl">URL de descarga prefirmada del archivo ZIP con los CSVs.</param>
/// <param name="ExpiresAtUtc">Momento de vencimiento de la URL, en UTC.</param>
public sealed record ExportMyDataResult(string DownloadUrl, DateTime ExpiresAtUtc);
