namespace Cauce.Api.Contracts.Identity;

/// <summary>
/// Cuerpo de la petición de reenvío del correo de verificación (acta A40). La respuesta es siempre 200
/// cuando la petición supera la validación, exista o no la cuenta, para no revelar qué correos están
/// registrados ni cuáles ya están verificados.
/// </summary>
/// <param name="Email">Correo electrónico de la cuenta que solicita el reenvío.</param>
public sealed record ResendVerificationEmailRequest(string Email);
