using MediatR;

namespace Cauce.Application.Identity.UseCases.GetMyConsentPdf;

/// <summary>
/// Consulta que genera el comprobante en PDF del consentimiento informado aceptado por el paciente
/// autenticado (US01 CA04).
/// </summary>
public sealed record GetMyConsentPdfQuery : IRequest<ConsentPdfResult>;

/// <summary>
/// Resultado de la generación del comprobante de consentimiento.
/// </summary>
/// <param name="Content">Contenido binario del PDF.</param>
/// <param name="FileName">Nombre de archivo sugerido para la descarga.</param>
public sealed record ConsentPdfResult(byte[] Content, string FileName);
