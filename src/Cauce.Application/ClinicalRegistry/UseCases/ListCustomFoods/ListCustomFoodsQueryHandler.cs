using Cauce.Application.ClinicalRegistry.Dtos;
using Cauce.Application.ClinicalRegistry.Mapping;
using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.ClinicalRegistry;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Domain.Identity;
using MediatR;

namespace Cauce.Application.ClinicalRegistry.UseCases.ListCustomFoods;

/// <summary>
/// Handler de la consulta de alimentos personalizados del paciente autenticado.
/// </summary>
public sealed class ListCustomFoodsQueryHandler : IRequestHandler<ListCustomFoodsQuery, IReadOnlyList<CustomFoodSummary>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly ICustomFoodRepository _customFoodRepository;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    public ListCustomFoodsQueryHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        ICustomFoodRepository customFoodRepository)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _customFoodRepository = customFoodRepository;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CustomFoodSummary>> Handle(ListCustomFoodsQuery request, CancellationToken cancellationToken)
    {
        var patientId = await ResolveCurrentPatientIdAsync(cancellationToken).ConfigureAwait(false);
        var customFoods = await _customFoodRepository.ListByPatientAsync(patientId, cancellationToken).ConfigureAwait(false);
        return customFoods.Select(ClinicalRegistryMappings.ToSummary).ToList();
    }

    private async Task<Guid> ResolveCurrentPatientIdAsync(CancellationToken ct)
    {
        var keycloakId = _currentUserService.UserId
            ?? throw new UnauthorizedAccessException("No hay un usuario autenticado en la petición.");

        var user = await _userRepository.FindByKeycloakIdAsync(keycloakId.ToString(), ct).ConfigureAwait(false)
            ?? throw new UnauthorizedAccessException("El usuario autenticado no tiene una cuenta local asociada.");

        var patientRoleId = await _userRepository.GetRoleIdAsync(UserRoles.Patient, ct).ConfigureAwait(false);
        if (user.RoleId != patientRoleId)
        {
            throw new UnauthorizedAccessException("Solo un paciente puede consultar sus alimentos personalizados.");
        }

        return user.Id;
    }
}
