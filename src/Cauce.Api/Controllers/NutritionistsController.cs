using Cauce.Application.Patients.UseCases.GetAssignedPatientDetail;
using Cauce.Application.Patients.UseCases.ListAssignedPatients;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cauce.Api.Controllers;

/// <summary>
/// Endpoints del nutricionista sobre sus pacientes asignados.
/// </summary>
[Route("api/v{version:apiVersion}/nutritionists")]
[Authorize(Policy = "Nutritionist")]
public sealed class NutritionistsController : BaseApiController
{
    private readonly ISender _mediator;

    /// <summary>
    /// Inicializa el controlador con el mediador de casos de uso.
    /// </summary>
    /// <param name="mediator">Mediador de MediatR.</param>
    public NutritionistsController(ISender mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Lista los pacientes activos asignados al nutricionista autenticado.
    /// </summary>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Los pacientes asignados.</returns>
    [HttpGet("me/patients")]
    public async Task<IActionResult> ListAssignedPatients(CancellationToken ct)
    {
        var result = await _mediator.Send(new ListAssignedPatientsQuery(), ct);
        return Ok(result);
    }

    /// <summary>
    /// Devuelve el detalle clínico de un paciente asignado. Requiere una asignación
    /// activa con el paciente; de lo contrario responde 403.
    /// </summary>
    /// <param name="patientUserId">Identificador de la cuenta del paciente.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>El detalle del paciente.</returns>
    [HttpGet("me/patients/{patientUserId:guid}")]
    public async Task<IActionResult> GetAssignedPatientDetail(Guid patientUserId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetAssignedPatientDetailQuery(patientUserId), ct);
        return Ok(result);
    }
}
