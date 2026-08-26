using MediatR;

namespace Cauce.Application.Identity.UseCases.GetCurrentConsent;

/// <summary>
/// Consulta que devuelve el documento de consentimiento informado vigente. Es anónima: el paciente
/// la invoca antes de registrarse, cuando todavía no tiene cuenta ni token (US01).
/// </summary>
public sealed record GetCurrentConsentQuery : IRequest<CurrentConsentResult>;

/// <summary>
/// Documento de consentimiento informado vigente.
/// </summary>
/// <param name="Version">Versión vigente del documento.</param>
/// <param name="Text">Texto íntegro del documento.</param>
/// <param name="Hash">Hash SHA-256 del texto, en hexadecimal minúscula (64 caracteres).</param>
public sealed record CurrentConsentResult(string Version, string Text, string Hash);
