using Cauce.Application.ClinicalRegistry.Dtos;
using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.ClinicalRegistry;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Domain.ClinicalRegistry.Exceptions;
using Cauce.Domain.Identity;
using MediatR;

namespace Cauce.Application.ClinicalRegistry.UseCases.CheckFoodAllergens;

/// <summary>
/// Handler de <see cref="CheckFoodAllergensQuery"/>. Usa el mismo <see cref="ICustomFoodAllergenChecker"/>
/// que el chequeo al guardar un alimento personalizado, para que el aviso y el guardado no se separen.
/// </summary>
public sealed class CheckFoodAllergensQueryHandler : IRequestHandler<CheckFoodAllergensQuery, FoodAllergenCheckResult>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly IFoodItemRepository _foodItemRepository;
    private readonly ICustomFoodAllergenChecker _allergenChecker;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    public CheckFoodAllergensQueryHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        IFoodItemRepository foodItemRepository,
        ICustomFoodAllergenChecker allergenChecker)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _foodItemRepository = foodItemRepository;
        _allergenChecker = allergenChecker;
    }

    /// <inheritdoc />
    public async Task<FoodAllergenCheckResult> Handle(CheckFoodAllergensQuery request, CancellationToken cancellationToken)
    {
        var patientId = await ResolveCurrentPatientIdAsync(cancellationToken).ConfigureAwait(false);

        var food = await _foodItemRepository.FindByIdAsync(request.FoodId, cancellationToken).ConfigureAwait(false);
        if (food is null || !food.IsActive)
        {
            throw new FoodItemNotFoundException(request.FoodId);
        }

        var detected = await _allergenChecker
            .CheckAsync(patientId, [food.Id], cancellationToken)
            .ConfigureAwait(false);
        var allergens = detected
            .Select(allergen => allergen.AllergenName)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        return new FoodAllergenCheckResult(food.Id, food.Name, allergens.Count > 0, allergens);
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
            throw new UnauthorizedAccessException("Solo un paciente puede consultar sus alergias.");
        }

        return user.Id;
    }
}
