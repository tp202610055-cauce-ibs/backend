using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.ClinicalRegistry;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Domain.Auditing.Enums;
using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry.Exceptions;
using Cauce.Domain.Identity;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cauce.Application.ClinicalRegistry.UseCases.UpdateCustomFood;

/// <summary>
/// Handler de la actualización de un alimento personalizado. Verifica la propiedad del
/// paciente, la unicidad del nombre y la existencia de los alimentos referenciados;
/// reemplaza el conjunto de ingredientes y registra el evento en auditoría.
/// </summary>
public sealed class UpdateCustomFoodCommandHandler : IRequestHandler<UpdateCustomFoodCommand, UpdateCustomFoodResult>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly ICustomFoodRepository _customFoodRepository;
    private readonly IFoodItemRepository _foodItemRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogger _auditLogger;
    private readonly ILogger<UpdateCustomFoodCommandHandler> _logger;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    public UpdateCustomFoodCommandHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        ICustomFoodRepository customFoodRepository,
        IFoodItemRepository foodItemRepository,
        IUnitOfWork unitOfWork,
        IAuditLogger auditLogger,
        ILogger<UpdateCustomFoodCommandHandler> logger)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _customFoodRepository = customFoodRepository;
        _foodItemRepository = foodItemRepository;
        _unitOfWork = unitOfWork;
        _auditLogger = auditLogger;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<UpdateCustomFoodResult> Handle(UpdateCustomFoodCommand request, CancellationToken cancellationToken)
    {
        var patientId = await ResolveCurrentPatientIdAsync(cancellationToken).ConfigureAwait(false);

        var customFood = await _customFoodRepository.FindByIdWithIngredientsAsync(request.CustomFoodId, cancellationToken).ConfigureAwait(false)
            ?? throw new CustomFoodNotFoundException(request.CustomFoodId);

        if (customFood.PatientId != patientId)
        {
            throw new PatientResourceAccessException();
        }

        if (!string.Equals(customFood.Name, request.Name, StringComparison.Ordinal)
            && await _customFoodRepository.ExistsByPatientAndNameAsync(patientId, request.Name, cancellationToken).ConfigureAwait(false))
        {
            throw new DuplicateCustomFoodException();
        }

        foreach (var ingredient in request.Ingredients)
        {
            var food = await _foodItemRepository.FindByIdAsync(ingredient.FoodId, cancellationToken).ConfigureAwait(false);
            if (food is null || !food.IsActive)
            {
                throw new FoodItemNotFoundException(ingredient.FoodId);
            }
        }

        customFood.UpdateName(request.Name);
        customFood.UpdatePortionSize(request.PortionSizeGrams);

        var currentFoodIds = customFood.Ingredients.Select(i => i.FoodId).ToList();
        foreach (var foodId in currentFoodIds)
        {
            customFood.RemoveIngredient(foodId);
        }

        foreach (var ingredient in request.Ingredients)
        {
            customFood.AddIngredient(ingredient.FoodId, ingredient.ProportionGrams);
        }

        // Auditoría explícita ANTES del SaveChanges: custom_foods no tiene trigger; una sola
        // transacción persiste los cambios y la bitácora de forma atómica (DEC-B5-01 capa 3, acta A8).
        await _auditLogger.LogAsync(
            AuditActionType.Update, nameof(CustomFood), customFood.Id,
            oldValuesHash: null, newValuesHash: null, additionalContext: null, cancellationToken).ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Custom food {CustomFoodId} updated.", customFood.Id);
        return new UpdateCustomFoodResult(customFood.Id);
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
            throw new UnauthorizedAccessException("Solo un paciente puede actualizar alimentos personalizados.");
        }

        return user.Id;
    }
}
