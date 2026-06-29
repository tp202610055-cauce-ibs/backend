using Cauce.Application.ClinicalRegistry.Dtos;
using Cauce.Application.Common.Models;
using Cauce.Domain.ClinicalRegistry.Enums;
using MediatR;

namespace Cauce.Application.ClinicalRegistry.UseCases.ListFoodItemsCatalog;

/// <summary>
/// Consulta paginada del catálogo de alimentos activos, con filtros opcionales por
/// categoría y nivel FODMAP. Accesible para cualquier usuario autenticado.
/// </summary>
/// <param name="Page">Número de página (base 1).</param>
/// <param name="PageSize">Tamaño de página.</param>
/// <param name="Category">Filtro por categoría, opcional.</param>
/// <param name="FodmapLevel">Filtro por nivel FODMAP, opcional.</param>
public sealed record ListFoodItemsCatalogQuery(
    int Page,
    int PageSize,
    string? Category,
    FodmapLevel? FodmapLevel) : IRequest<PagedResult<FoodItemSummary>>;
