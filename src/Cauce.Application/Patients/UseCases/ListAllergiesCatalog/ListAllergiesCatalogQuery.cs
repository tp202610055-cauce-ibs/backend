using MediatR;

namespace Cauce.Application.Patients.UseCases.ListAllergiesCatalog;

/// <summary>
/// Consulta que devuelve el catálogo de alergias activas. Accesible para cualquier
/// usuario autenticado.
/// </summary>
public sealed record ListAllergiesCatalogQuery : IRequest<IReadOnlyList<AllergyCatalogItem>>;
