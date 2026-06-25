namespace Cauce.Api.Contracts.Identity;

/// <summary>
/// Cuerpo de la petición de registro de paciente. La dirección IP no se recibe del
/// cliente: el backend la deriva del contexto de la conexión.
/// </summary>
/// <param name="Email">Correo electrónico del paciente.</param>
/// <param name="FullName">Nombre completo del paciente.</param>
/// <param name="Password">Contraseña elegida por el paciente.</param>
/// <param name="ConsentDocumentVersion">Versión del documento de consentimiento aceptado.</param>
/// <param name="ConsentTextHash">Hash SHA-256 (hex) del texto de consentimiento aceptado.</param>
/// <param name="InvitationCode">Código de invitación opcional.</param>
public sealed record RegisterPatientRequest(
    string Email,
    string FullName,
    string Password,
    string ConsentDocumentVersion,
    string ConsentTextHash,
    string? InvitationCode);
