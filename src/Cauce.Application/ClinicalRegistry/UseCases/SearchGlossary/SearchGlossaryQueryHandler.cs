using Cauce.Application.ClinicalRegistry.Dtos;
using Cauce.Application.ClinicalRegistry.Mapping;
using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.ClinicalRegistry;
using Cauce.Domain.Identity;
using MediatR;

namespace Cauce.Application.ClinicalRegistry.UseCases.SearchGlossary;

/// <summary>
/// Handler de la búsqueda del glosario (US27). Devuelve las coincidencias con la definición apropiada
/// al rol del solicitante. Una búsqueda vacía devuelve el glosario completo ordenado.
/// </summary>
public sealed class SearchGlossaryQueryHandler : IRequestHandler<SearchGlossaryQuery, GlossaryResult>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IGlossaryRepository _glossaryRepository;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    public SearchGlossaryQueryHandler(ICurrentUserService currentUserService, IGlossaryRepository glossaryRepository)
    {
        _currentUserService = currentUserService;
        _glossaryRepository = glossaryRepository;
    }

    /// <inheritdoc />
    public async Task<GlossaryResult> Handle(SearchGlossaryQuery request, CancellationToken cancellationToken)
    {
        var terms = string.IsNullOrWhiteSpace(request.Query)
            ? await _glossaryRepository.ListAllOrderedAsync(cancellationToken).ConfigureAwait(false)
            : await _glossaryRepository.SearchAsync(request.Query.Trim(), cancellationToken).ConfigureAwait(false);

        var isNutritionist = _currentUserService.Roles.Contains(UserRoles.Nutritionist, StringComparer.OrdinalIgnoreCase);
        return GlossaryMappings.ToResult(terms, isNutritionist);
    }
}
