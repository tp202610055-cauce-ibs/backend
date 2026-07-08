using Cauce.Application.ClinicalRegistry.Dtos;
using Cauce.Application.ClinicalRegistry.Mapping;
using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.ClinicalRegistry;
using Cauce.Domain.Identity;
using MediatR;

namespace Cauce.Application.ClinicalRegistry.UseCases.GetGlossary;

/// <summary>
/// Handler que devuelve el glosario completo (US27). Elige la definición según el rol del solicitante
/// tomado del JWT (nutricionista o paciente).
/// </summary>
public sealed class GetGlossaryQueryHandler : IRequestHandler<GetGlossaryQuery, GlossaryResult>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IGlossaryRepository _glossaryRepository;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    public GetGlossaryQueryHandler(ICurrentUserService currentUserService, IGlossaryRepository glossaryRepository)
    {
        _currentUserService = currentUserService;
        _glossaryRepository = glossaryRepository;
    }

    /// <inheritdoc />
    public async Task<GlossaryResult> Handle(GetGlossaryQuery request, CancellationToken cancellationToken)
    {
        var terms = await _glossaryRepository.ListAllOrderedAsync(cancellationToken).ConfigureAwait(false);
        var isNutritionist = _currentUserService.Roles.Contains(UserRoles.Nutritionist, StringComparer.OrdinalIgnoreCase);
        return GlossaryMappings.ToResult(terms, isNutritionist);
    }
}
