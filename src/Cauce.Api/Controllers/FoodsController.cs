using Cauce.Api.Configuration;
using Cauce.Application.ClinicalRegistry.Dtos;
using Cauce.Application.ClinicalRegistry.UseCases.GetFoodItemDetail;
using Cauce.Application.ClinicalRegistry.UseCases.GetFoodSuggestions;
using Cauce.Application.ClinicalRegistry.UseCases.ListFoodItemsCatalog;
using Cauce.Application.ClinicalRegistry.UseCases.SearchFoodItems;
using Cauce.Application.Common.Models;
using Cauce.Domain.ClinicalRegistry.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cauce.Api.Controllers;

/// <summary>
/// Endpoints de consulta del catálogo de alimentos. Accesibles para cualquier usuario
/// autenticado (paciente o nutricionista).
/// </summary>
[Route("api/v{version:apiVersion}/foods")]
[Authorize]
[EnableRateLimiting(RateLimitingPolicies.DefaultAuthenticated)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public sealed class FoodsController : BaseApiController
{
    private readonly ISender _mediator;

    /// <summary>
    /// Inicializa el controlador con el mediador de casos de uso.
    /// </summary>
    /// <param name="mediator">Mediador de MediatR.</param>
    public FoodsController(ISender mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Lista el catálogo de alimentos activos de forma paginada, con filtros opcionales.
    /// </summary>
    /// <param name="page">Número de página.</param>
    /// <param name="pageSize">Tamaño de página.</param>
    /// <param name="category">Filtro por categoría, opcional.</param>
    /// <param name="fodmapLevel">Filtro por nivel FODMAP, opcional.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>El catálogo paginado.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<FoodItemSummary>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? category = null,
        [FromQuery] FodmapLevel? fodmapLevel = null,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new ListFoodItemsCatalogQuery(page, pageSize, category, fodmapLevel), ct);
        return Ok(result);
    }

    /// <summary>
    /// Busca alimentos del catálogo por coincidencia de nombre.
    /// </summary>
    /// <param name="q">Texto a buscar.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Los alimentos coincidentes.</returns>
    [HttpGet("search")]
    [ProducesResponseType(typeof(IReadOnlyList<FoodItemSummary>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Search([FromQuery] string q, CancellationToken ct)
    {
        var result = await _mediator.Send(new SearchFoodItemsQuery(q), ct);
        return Ok(result);
    }

    /// <summary>
    /// Devuelve sugerencias de alimentos para el paciente autenticado (US09 CA03): frecuentes en los
    /// últimos 30 días, registrados en las últimas 24 horas y una selección rotativa del catálogo.
    /// </summary>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>Las tres listas de sugerencias.</returns>
    [HttpGet("suggestions")]
    [Authorize(Policy = "Patient")]
    [ProducesResponseType(typeof(FoodSuggestionsResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Suggestions(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetFoodSuggestionsQuery(), ct);
        return Ok(result);
    }

    /// <summary>
    /// Devuelve el detalle de un alimento del catálogo.
    /// </summary>
    /// <param name="foodId">Identificador del alimento.</param>
    /// <param name="ct">Token de cancelación.</param>
    /// <returns>El detalle del alimento.</returns>
    [HttpGet("{foodId:guid}")]
    [ProducesResponseType(typeof(FoodItemDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDetail(Guid foodId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetFoodItemDetailQuery(foodId), ct);
        return Ok(result);
    }
}
