using Cauce.Application.Common.Interfaces.Patients;
using MediatR;

namespace Cauce.Application.Patients.UseCases.ListAllergiesCatalog;

/// <summary>
/// Handler que devuelve el catálogo de alergias activas.
/// </summary>
public sealed class ListAllergiesCatalogQueryHandler : IRequestHandler<ListAllergiesCatalogQuery, IReadOnlyList<AllergyCatalogItem>>
{
    private readonly IAllergyRepository _allergyRepository;

    /// <summary>
    /// Inicializa el handler con el repositorio de alergias.
    /// </summary>
    /// <param name="allergyRepository">Repositorio del catálogo de alergias.</param>
    public ListAllergiesCatalogQueryHandler(IAllergyRepository allergyRepository)
    {
        _allergyRepository = allergyRepository;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AllergyCatalogItem>> Handle(ListAllergiesCatalogQuery request, CancellationToken cancellationToken)
    {
        var allergies = await _allergyRepository.ListActiveAsync(cancellationToken).ConfigureAwait(false);
        return allergies
            .Select(a => new AllergyCatalogItem(a.Id, a.Name, a.AllergyType, a.Description))
            .ToList();
    }
}
