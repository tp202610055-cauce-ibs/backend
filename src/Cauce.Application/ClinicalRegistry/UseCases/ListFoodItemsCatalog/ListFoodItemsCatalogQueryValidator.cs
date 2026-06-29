using FluentValidation;

namespace Cauce.Application.ClinicalRegistry.UseCases.ListFoodItemsCatalog;

/// <summary>
/// Validador del rango de paginación de <see cref="ListFoodItemsCatalogQuery"/>.
/// </summary>
public sealed class ListFoodItemsCatalogQueryValidator : AbstractValidator<ListFoodItemsCatalogQuery>
{
    /// <summary>
    /// Configura las reglas de validación.
    /// </summary>
    public ListFoodItemsCatalogQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.FodmapLevel).IsInEnum().When(x => x.FodmapLevel.HasValue);
    }
}
