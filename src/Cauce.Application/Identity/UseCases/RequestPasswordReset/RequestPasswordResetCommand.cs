using MediatR;

namespace Cauce.Application.Identity.UseCases.RequestPasswordReset;

/// <summary>
/// Comando para solicitar el restablecimiento de contraseña. Por seguridad, la
/// respuesta es siempre exitosa, exista o no la cuenta, para no filtrar la
/// existencia de correos registrados.
/// </summary>
/// <param name="Email">Correo electrónico de la cuenta.</param>
/// <param name="IpAddress">Dirección IP de origen, o <see langword="null"/>.</param>
public sealed record RequestPasswordResetCommand(
    string Email,
    string? IpAddress) : IRequest;
