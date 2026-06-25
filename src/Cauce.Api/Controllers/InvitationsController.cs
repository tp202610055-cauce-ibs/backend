using Cauce.Application.Common.Interfaces;
using Cauce.Application.Identity.UseCases.GenerateInvitationCode;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cauce.Api.Controllers;

/// <summary>
/// Endpoints de gestión de códigos de invitación, reservados a nutricionistas.
/// </summary>
[Route("api/v{version:apiVersion}/invitations")]
[Authorize(Policy = "Nutritionist")]
public sealed class InvitationsController : BaseApiController
{
    private readonly ISender _mediator;
    private readonly ICurrentUserService _currentUserService;

    /// <summary>
    /// Inicializa el controlador con sus dependencias.
    /// </summary>
    /// <param name="mediator">Mediador de MediatR.</param>
    /// <param name="currentUserService">Servicio del usuario autenticado actual.</param>
    public InvitationsController(ISender mediator, ICurrentUserService currentUserService)
    {
        _mediator = mediator;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Genera un nuevo código de invitación para el nutricionista autenticado.
    /// </summary>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>El código generado y su fecha de expiración, con código 201.</returns>
    [HttpPost]
    public async Task<IActionResult> Generate(CancellationToken ct)
    {
        var nutritionistKeycloakId = _currentUserService.UserId;
        if (nutritionistKeycloakId is null)
        {
            return Unauthorized();
        }

        var result = await _mediator.Send(new GenerateInvitationCodeCommand(nutritionistKeycloakId.Value), ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }
}
