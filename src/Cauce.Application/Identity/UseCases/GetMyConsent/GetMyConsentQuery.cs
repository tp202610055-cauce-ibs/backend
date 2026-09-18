using MediatR;

namespace Cauce.Application.Identity.UseCases.GetMyConsent;

/// <summary>
/// Consulta los datos del consentimiento informado que aceptó el paciente autenticado.
/// </summary>
public sealed record GetMyConsentQuery : IRequest<MyConsentResult>;

/// <summary>
/// Datos del consentimiento aceptado, para mostrarlos sin descargar el PDF (HU0001 escenario 4).
/// </summary>
/// <param name="DocumentVersion">Versión del documento que el paciente aceptó.</param>
/// <param name="AcceptedAt">Momento de aceptación, en UTC.</param>
/// <param name="ConsentTextHash">Hash SHA-256 (hex) del texto aceptado.</param>
/// <param name="TextAvailable">
/// Indica si el texto de esa versión sigue disponible en <c>consent_documents</c>. Es
/// <see langword="false"/> para aceptaciones anteriores a la introducción de la tabla, cuyo
/// texto no quedó guardado y por lo tanto no puede reproducirse en el PDF.
/// </param>
public sealed record MyConsentResult(
    string DocumentVersion,
    DateTime AcceptedAt,
    string ConsentTextHash,
    bool TextAvailable);
