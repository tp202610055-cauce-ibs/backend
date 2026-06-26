using MediatR;

namespace Cauce.Application.Identity.UseCases.RegisterPatient;

/// <summary>
/// Comando para registrar un nuevo paciente. La gestión de credenciales se delega
/// a Keycloak; el backend persiste el usuario, su consentimiento y, si aplica,
/// marca el código de invitación como usado.
/// </summary>
/// <param name="Email">Correo electrónico del paciente.</param>
/// <param name="FullName">Nombre completo del paciente.</param>
/// <param name="Password">Contraseña elegida por el paciente.</param>
/// <param name="ConsentDocumentVersion">Versión del documento de consentimiento aceptado.</param>
/// <param name="ConsentTextHash">Hash SHA-256 (hex) del texto de consentimiento aceptado.</param>
/// <param name="IpAddress">Dirección IP de origen, o <see langword="null"/>.</param>
/// <param name="InvitationCode">Código de invitación opcional.</param>
public sealed record RegisterPatientCommand(
    string Email,
    string FullName,
    string Password,
    string ConsentDocumentVersion,
    string ConsentTextHash,
    string? IpAddress,
    string? InvitationCode) : IRequest<RegisterPatientResult>;
