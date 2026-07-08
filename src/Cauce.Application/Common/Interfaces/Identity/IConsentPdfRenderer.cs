namespace Cauce.Application.Common.Interfaces.Identity;

/// <summary>
/// Renderiza en PDF el comprobante del consentimiento informado aceptado por un paciente (US01 CA04).
/// El PDF es un dato propio del paciente y se entrega sin cifrar.
/// </summary>
public interface IConsentPdfRenderer
{
    /// <summary>
    /// Renderiza el comprobante de consentimiento en PDF.
    /// </summary>
    /// <param name="content">Datos del encabezado y del consentimiento aceptado.</param>
    /// <returns>El contenido binario del PDF.</returns>
    byte[] Render(ConsentPdfContent content);
}

/// <summary>
/// Datos que componen el comprobante de consentimiento en PDF.
/// </summary>
/// <param name="PatientFullName">Nombre completo del paciente.</param>
/// <param name="PatientEmail">Correo del paciente.</param>
/// <param name="DocumentVersion">Versión del documento de consentimiento aceptado.</param>
/// <param name="AcceptedAtUtc">Momento de aceptación, en UTC.</param>
/// <param name="ConsentTextHash">Hash SHA-256 (hex) del texto aceptado.</param>
/// <param name="IpAddress">Dirección IP de origen de la aceptación, o <see langword="null"/>.</param>
public sealed record ConsentPdfContent(
    string PatientFullName,
    string PatientEmail,
    string DocumentVersion,
    DateTime AcceptedAtUtc,
    string ConsentTextHash,
    string? IpAddress);
