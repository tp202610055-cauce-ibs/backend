using Cauce.Domain.Patients.Enums;

namespace Cauce.Application.Patients.UseCases.ListAllergiesCatalog;

/// <summary>
/// Entrada del catálogo de alergias para listados.
/// </summary>
/// <param name="AllergyId">Identificador de la alergia.</param>
/// <param name="Name">Nombre de la alergia.</param>
/// <param name="AllergyType">Tipo de reacción.</param>
/// <param name="Description">Descripción.</param>
public sealed record AllergyCatalogItem(
    Guid AllergyId,
    string Name,
    AllergyType AllergyType,
    string? Description);
