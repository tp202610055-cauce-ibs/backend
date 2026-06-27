using Cauce.Api.Contracts.Patients;
using Cauce.Application.Patients.UseCases.CreatePatientProfile;
using Cauce.Application.Patients.UseCases.DeclarePatientAllergy;
using Cauce.Application.Patients.UseCases.GetPatientProfile;
using Cauce.Application.Patients.UseCases.ListPatientAllergies;
using Cauce.Application.Patients.UseCases.RemovePatientAllergy;
using Cauce.Application.Patients.UseCases.UpdatePatientProfile;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cauce.Api.Controllers;

/// <summary>
/// Endpoints del paciente sobre su propio perfil clínico y sus alergias.
/// </summary>
[Route("api/v{version:apiVersion}/patients")]
[Authorize(Policy = "Patient")]
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
    public async Task<IActionResult> RemoveAllergy(Guid patientAllergyId, CancellationToken ct)
    {
        await _mediator.Send(new RemovePatientAllergyCommand(patientAllergyId), ct);
        return NoContent();
    }
}
