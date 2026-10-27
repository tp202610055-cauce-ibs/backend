using Cauce.Application.ClinicalRegistry.Dtos;
using Cauce.Application.ClinicalRegistry.UseCases.GetIbsSssCycleForNutritionist;
using Cauce.Application.ClinicalRegistry.UseCases.GetPatientHistoryForNutritionist;
using Cauce.Application.Common.Models;
using Cauce.Application.Patients.Dtos;
using Cauce.Application.Patients.UseCases.GetAssignedPatientDetail;
using Cauce.Application.Patients.UseCases.GetPatientEvolutionForNutritionist;
using Cauce.Application.Patients.UseCases.ListAssignedPatients;
using Cauce.Application.Recommendations.Dtos;
using Cauce.Application.Recommendations.UseCases.ListPatientRecommendationsForNutritionist;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Cauce.Api.Controllers;

/// <summary>
/// Endpoints del nutricionista sobre sus pacientes asignados.
/// </summary>
[Route("api/v{version:apiVersion}/nutritionists")]
[Authorize(Policy = "Nutritionist")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
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
    [ProducesResponseType(typeof(IReadOnlyList<AssignedPatientSummary>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAssignedPatients(CancellationToken ct)
    {
        var result = await _mediator.Send(new ListAssignedPatientsQuery(), ct);
        return Ok(result);
    }

    /// <summary>
    /// Devuelve el detalle clínico de un paciente asignado. Requiere una asignación
    /// activa con el paciente; de lo contrario responde 403. Si el paciente todavía no completó
    /// su perfil, responde 200 con <c>onboardingCompleted: false</c> y los campos clínicos en null
    /// (acta A69).
    /// </summary>
    /// <param name="patientUserId">Identificador de la cuenta del paciente.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>El detalle del paciente.</returns>
    [HttpGet("me/patients/{patientUserId:guid}")]
    [ProducesResponseType(typeof(GetAssignedPatientDetailResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAssignedPatientDetail(Guid patientUserId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetAssignedPatientDetailQuery(patientUserId), ct);
        return Ok(result);
    }

    /// <summary>
    /// Devuelve las métricas de evolución clínica de un paciente asignado (US21): serie
    /// IBS-SSS, variación respecto de la línea base, respuesta clínica significativa y
    /// frecuencia de registro reciente. Requiere una asignación activa; de lo contrario 403.
    /// </summary>
    /// <param name="patientId">Identificador de la cuenta del paciente.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Las métricas de evolución del paciente.</returns>
    [HttpGet("me/patients/{patientId:guid}/evolution")]
    [ProducesResponseType(typeof(PatientEvolutionForNutritionistResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPatientEvolution(Guid patientId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetPatientEvolutionForNutritionistQuery(patientId), ct);
        return Ok(result);
    }

    /// <summary>
    /// Devuelve el diario de un paciente asignado (comidas, síntomas y notas clínicas) en un rango de
    /// fechas, del más reciente al más antiguo. Requiere una asignación activa y queda auditado.
    /// </summary>
    /// <remarks>Devuelve como máximo 500 comidas y 500 síntomas del rango; las notas clínicas no tienen tope.</remarks>
    /// <param name="patientId">Identificador de la cuenta del paciente.</param>
    /// <param name="from">Inicio del rango, obligatorio e inclusivo, con zona horaria (<c>Z</c> u offset). Filtra comidas y síntomas por <c>clientCreatedAt</c> y notas por <c>createdAt</c>.</param>
    /// <param name="to">Fin del rango, obligatorio e inclusivo, con zona horaria y no anterior a <c>from</c>.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Los eventos del diario del paciente.</returns>
    [HttpGet("me/patients/{patientId:guid}/history")]
    [ProducesResponseType(typeof(IReadOnlyList<HistoryEvent>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetPatientHistory(
        Guid patientId,
        [FromQuery, BindRequired] DateTime from,
        [FromQuery, BindRequired] DateTime to,
        CancellationToken ct)
    {
        var result = await _mediator.Send(new GetPatientHistoryForNutritionistQuery(patientId, from, to), ct);
        return Ok(result);
    }

    /// <summary>
    /// Lista, paginadas, todas las recomendaciones de un paciente asignado: vigentes, en revisión, con
    /// decisión final y archivadas. Requiere una asignación activa y queda auditado.
    /// </summary>
    /// <param name="patientId">Identificador de la cuenta del paciente.</param>
    /// <param name="page">Número de página.</param>
    /// <param name="pageSize">Tamaño de página, de 1 a 100.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>La página de recomendaciones del paciente.</returns>
    [HttpGet("me/patients/{patientId:guid}/recommendations")]
    [ProducesResponseType(typeof(PagedResult<PatientRecommendationForNutritionistDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ListPatientRecommendations(
        Guid patientId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new ListPatientRecommendationsForNutritionistQuery(patientId, page, pageSize), ct);
        return Ok(result);
    }

    /// <summary>
    /// Devuelve el estado del ciclo vigente del cuestionario IBS-SSS de un paciente asignado. Requiere
    /// una asignación activa y queda auditado.
    /// </summary>
    /// <param name="patientId">Identificador de la cuenta del paciente.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>El estado del ciclo.</returns>
    [HttpGet("me/patients/{patientId:guid}/ibs-sss-cycle")]
    [ProducesResponseType(typeof(IbsSssCycleStatusDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPatientIbsSssCycle(Guid patientId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetIbsSssCycleForNutritionistQuery(patientId), ct);
        return Ok(result);
    }
}
