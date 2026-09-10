using Cauce.Api.Contracts.Patients;
using Cauce.Application.Identity.UseCases.GetMyConsentPdf;
using Cauce.Application.Patients.Dtos;
using Cauce.Application.Patients.UseCases.AssignNutritionist;
using Cauce.Application.Patients.UseCases.CreatePatientProfile;
using Cauce.Application.Patients.UseCases.DeclarePatientAllergy;
using Cauce.Application.Patients.UseCases.DeleteMyAccount;
using Cauce.Application.Patients.UseCases.ExportMyData;
using Cauce.Application.Patients.UseCases.GetMyProfileSummary;
using Cauce.Application.Patients.UseCases.GetPatientProfile;
using Cauce.Application.Patients.UseCases.ListPatientAllergies;
using Cauce.Application.Patients.UseCases.RemovePatientAllergy;
using Cauce.Application.Patients.UseCases.UpdatePatientProfile;
using Cauce.Application.Reports.UseCases.GenerateMyClinicalReport;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cauce.Api.Controllers;

/// <summary>
/// Endpoints del paciente sobre su propio perfil clínico y sus alergias.
/// </summary>
[Route("api/v{version:apiVersion}/patients")]
[Authorize(Policy = "Patient")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public sealed class PatientsController : BaseApiController
{
    private readonly ISender _mediator;

    /// <summary>
    /// Inicializa el controlador con el mediador de casos de uso.
    /// </summary>
    /// <param name="mediator">Mediador de MediatR.</param>
    public PatientsController(ISender mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Crea el perfil clínico del paciente autenticado.
    /// </summary>
    /// <param name="request">Datos del perfil.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>El resultado de la creación, con código 201.</returns>
    [HttpPost("profile")]
    [ProducesResponseType(typeof(CreatePatientProfileResult), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateProfile([FromBody] CreatePatientProfileRequest request, CancellationToken ct)
    {
        var command = new CreatePatientProfileCommand(
            request.DateOfBirth,
            request.BiologicalSex,
            request.WeightKg,
            request.HeightCm,
            request.IbsSubtype,
            request.DiagnosisDate,
            request.Medications);

        var result = await _mediator.Send(command, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>
    /// Actualiza campos modificables del perfil clínico del paciente autenticado.
    /// </summary>
    /// <param name="request">Campos a actualizar.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>El perfil con valores recalculados, con código 200.</returns>
    [HttpPut("profile")]
    [ProducesResponseType(typeof(UpdatePatientProfileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdatePatientProfileRequest request, CancellationToken ct)
    {
        var command = new UpdatePatientProfileCommand(
            request.WeightKg,
            request.HeightCm,
            request.IbsSubtype,
            request.DiagnosisDate,
            request.Medications);

        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }

    /// <summary>
    /// Devuelve el perfil clínico completo del paciente autenticado.
    /// </summary>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>El perfil clínico.</returns>
    [HttpGet("profile")]
    [ProducesResponseType(typeof(GetPatientProfileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfile(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetPatientProfileQuery(), ct);
        return Ok(result);
    }

    /// <summary>
    /// Lista las alergias declaradas por el paciente autenticado.
    /// </summary>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Las alergias declaradas.</returns>
    [HttpGet("allergies")]
    [ProducesResponseType(typeof(IReadOnlyList<PatientAllergySummary>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAllergies(CancellationToken ct)
    {
        var result = await _mediator.Send(new ListPatientAllergiesQuery(), ct);
        return Ok(result);
    }

    /// <summary>
    /// Declara una alergia para el paciente autenticado.
    /// </summary>
    /// <param name="request">Datos de la alergia.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>El identificador de la declaración, con código 201.</returns>
    [HttpPost("allergies")]
    [ProducesResponseType(typeof(DeclarePatientAllergyResult), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeclareAllergy([FromBody] DeclareAllergyRequest request, CancellationToken ct)
    {
        var command = new DeclarePatientAllergyCommand(request.AllergyId, request.Severity, request.Notes);
        var result = await _mediator.Send(command, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>
    /// Elimina una declaración de alergia del paciente autenticado.
    /// </summary>
    /// <param name="patientAllergyId">Identificador de la declaración.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>204 si se eliminó correctamente.</returns>
    [HttpDelete("allergies/{patientAllergyId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveAllergy(Guid patientAllergyId, CancellationToken ct)
    {
        await _mediator.Send(new RemovePatientAllergyCommand(patientAllergyId), ct);
        return NoContent();
    }

    /// <summary>
    /// Exporta todos los datos personales y clínicos del paciente autenticado en un archivo ZIP de CSVs
    /// (US25, derecho a la portabilidad de datos, Ley N° 29733) y devuelve una URL de descarga prefirmada.
    /// </summary>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>La URL de descarga prefirmada y su vencimiento, con código 200.</returns>
    [HttpGet("me/export-data")]
    [ProducesResponseType(typeof(ExportMyDataResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportMyData(CancellationToken ct)
    {
        var result = await _mediator.Send(new ExportMyDataCommand(), ct);
        return Ok(result);
    }

    /// <summary>
    /// Descarga el comprobante en PDF del consentimiento informado aceptado por el paciente autenticado
    /// (US01 CA04). El PDF no está cifrado: es un dato propio del paciente.
    /// </summary>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>El PDF del consentimiento (<c>application/pdf</c>), o 404 si no hay consentimiento vigente.</returns>
    [HttpGet("me/consent/pdf")]
    [Produces("application/pdf")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetConsentPdf(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetMyConsentPdfQuery(), ct);
        return File(result.Content, "application/pdf", result.FileName);
    }

    /// <summary>
    /// Elimina (anonimiza) la cuenta del paciente autenticado en ejercicio del derecho al olvido
    /// (US26, Ley N° 29733). No es un borrado físico: los datos personales se anonimizan y la cuenta se
    /// deshabilita en el proveedor de identidad.
    /// </summary>
    /// <param name="confirmedActivePilotAcknowledged">
    /// Acuse de la retención normativa cuando la cuenta está inscrita en un piloto clínico activo. Si
    /// la cuenta está en el piloto y este valor es <see langword="false"/>, la operación devuelve 409.
    /// </param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>204 si la cuenta se anonimizó correctamente.</returns>
    [HttpDelete("me")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteMyAccount(
        [FromQuery] bool confirmedActivePilotAcknowledged,
        CancellationToken ct)
    {
        await _mediator.Send(new DeleteMyAccountCommand(confirmedActivePilotAcknowledged), ct);
        return NoContent();
    }

    /// <summary>
    /// Devuelve el perfil agregado del paciente autenticado (US28): identificación, perfil clínico,
    /// fecha de inicio en el piloto, nutricionista asignado y evolución IBS-SSS resumida.
    /// </summary>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>El perfil agregado.</returns>
    [HttpGet("me/summary")]
    [ProducesResponseType(typeof(MyProfileSummaryResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMySummary(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetMyProfileSummaryQuery(), ct);
        return Ok(result);
    }

    /// <summary>
    /// Genera el reporte clínico personal del paciente autenticado (US24), cubriendo sus últimos 90
    /// días. Devuelve una URL prefirmada de descarga; la contraseña del PDF cifrado se envía por correo
    /// en un mensaje separado. Responde 422 si el paciente no tiene datos en el período.
    /// </summary>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>La URL de descarga prefirmada y su vencimiento, con código 200.</returns>
    [HttpPost("me/report")]
    [ProducesResponseType(typeof(GenerateMyClinicalReportResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GenerateMyReport(CancellationToken ct)
    {
        var result = await _mediator.Send(new GenerateMyClinicalReportCommand(), ct);
        return Ok(result);
    }

    /// <summary>
    /// Canjea un código de invitación para vincular al paciente autenticado con un nutricionista
    /// (US20 CA02, acta A41). Está pensado para quienes se registraron sin código y no tenían forma de
    /// vincularse después.
    /// </summary>
    /// <param name="request">Código de invitación a canjear.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Los datos del nutricionista asignado, con código 201.</returns>
    /// <remarks>
    /// El 409 <c>nutritionist_not_available</c> incluye la extensión <c>reason</c> con el estado exacto
    /// de la cuenta del nutricionista: <c>pending_activation</c>, <c>inactive</c> o <c>suspended</c>.
    /// En ese caso el código <b>no</b> se consume.
    /// </remarks>
    [HttpPost("me/nutritionist-assignment")]
    [ProducesResponseType(typeof(AssignNutritionistResult), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AssignNutritionist(
        [FromBody] AssignNutritionistRequest request,
        CancellationToken ct)
    {
        // Se normaliza antes de validar para que el formato de mayúsculas del código no dependa de cómo
        // lo haya tipeado el paciente.
        var normalizedCode = request.InvitationCode?.Trim().ToUpperInvariant() ?? string.Empty;
        var result = await _mediator.Send(new AssignNutritionistCommand(normalizedCode), ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }
}
