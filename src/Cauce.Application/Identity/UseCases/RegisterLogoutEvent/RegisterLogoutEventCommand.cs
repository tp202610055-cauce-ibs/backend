using MediatR;

namespace Cauce.Application.Identity.UseCases.RegisterLogoutEvent;

/// <summary>
/// Comando para registrar el cierre de sesión del usuario autenticado. No lleva
/// payload: la identidad se obtiene del contexto de la petición.
/// </summary>
public sealed record RegisterLogoutEventCommand : IRequest;
