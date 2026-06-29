namespace Cauce.Application.Common.Models;

/// <summary>
/// Resultado paginado de una consulta: la página de elementos junto con los metadatos
/// de paginación necesarios para que el cliente reconstruya la navegación.
/// </summary>
/// <typeparam name="T">Tipo de los elementos de la página.</typeparam>
/// <param name="Items">Elementos de la página actual.</param>
/// <param name="Page">Número de página (base 1).</param>
/// <param name="PageSize">Tamaño de página solicitado.</param>
/// <param name="TotalCount">Cantidad total de elementos disponibles.</param>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
