using MediatR;

namespace Cauce.Application.Identity.UseCases.RegisterLoginEvent;

/// <summary>
/// Comando para registrar el inicio de sesión del usuario autenticado. No lleva
/// payload: la identidad se obtiene del contexto de la petición.
/// </summary>
public sealed record RegisterLoginEventCommand : IRequest;
