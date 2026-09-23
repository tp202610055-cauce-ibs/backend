using Cauce.Application.ClinicalRegistry.Dtos;
using Cauce.Application.ClinicalRegistry.Mapping;
using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.ClinicalRegistry;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Common.Models;
using Cauce.Domain.Identity;
using MediatR;

namespace Cauce.Application.ClinicalRegistry.UseCases.GetMealHistory;

/// <summary>
/// Handler de la consulta paginada del historial de comidas del paciente autenticado.
/// </summary>
public sealed class GetMealHistoryQueryHandler : IRequestHandler<GetMealHistoryQuery, PagedResult<MealHistoryItem>>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly IMealRepository _mealRepository;
    private readonly IMealFodmapResolver _fodmapResolver;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    public GetMealHistoryQueryHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        IMealRepository mealRepository,
        IMealFodmapResolver fodmapResolver)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _mealRepository = mealRepository;
        _fodmapResolver = fodmapResolver;
    }

    /// <inheritdoc />
    public async Task<PagedResult<MealHistoryItem>> Handle(GetMealHistoryQuery request, CancellationToken cancellationToken)
    {
        var patientId = await ResolveCurrentPatientIdAsync(cancellationToken).ConfigureAwait(false);
        var skip = (request.Page - 1) * request.PageSize;

        var meals = await _mealRepository
            .ListByPatientInRangeAsync(patientId, request.From, request.To, skip, request.PageSize, cancellationToken)
            .ConfigureAwait(false);

        var total = await _mealRepository
            .CountByPatientInRangeAsync(patientId, request.From, request.To, cancellationToken)
            .ConfigureAwait(false);

        // El nivel FODMAP agregado viaja también en la lectura, con la misma regla que en el alta.
        var fodmapByMeal = await _fodmapResolver.ResolveAsync(meals, cancellationToken).ConfigureAwait(false);
        var items = meals
            .Select(meal => ClinicalRegistryMappings.ToHistoryItem(
                meal,
                fodmapByMeal.TryGetValue(meal.Id, out var level) ? level : null))
            .ToList();
        return new PagedResult<MealHistoryItem>(items, request.Page, request.PageSize, total);
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
            throw new UnauthorizedAccessException("Solo un paciente puede consultar su historial de comidas.");
        }

        return user.Id;
    }
}
