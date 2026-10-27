using MediatR;

namespace Cauce.Application.Identity.UseCases.ValidatePasswordResetToken;

/// <summary>
/// Consulta que comprueba si un token de restablecimiento todavía sirve, sin consumirlo.
/// </summary>
/// <param name="Token">Token de restablecimiento en claro.</param>
public sealed record ValidatePasswordResetTokenQuery(string? Token) : IRequest;
