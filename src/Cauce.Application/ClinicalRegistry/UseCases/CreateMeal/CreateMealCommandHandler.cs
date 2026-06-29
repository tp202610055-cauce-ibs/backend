using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.ClinicalRegistry;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry.Enums;
using Cauce.Domain.ClinicalRegistry.Exceptions;
using Cauce.Domain.Identity;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cauce.Application.ClinicalRegistry.UseCases.CreateMeal;

/// <summary>
/// Handler del registro de una comida. Valida que los alimentos referenciados existan y
/// estén activos, y que los alimentos personalizados pertenezcan al paciente; persiste
/// la comida con sus ítems de forma atómica. No registra el evento en auditoría por su
/// alto volumen.
/// </summary>
public sealed class CreateMealCommandHandler : IRequestHandler<CreateMealCommand, CreateMealResult>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly IMealRepository _mealRepository;
    private readonly IFoodItemRepository _foodItemRepository;
    private readonly ICustomFoodRepository _customFoodRepository;
    private readonly IFodmapAggregator _fodmapAggregator;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateMealCommandHandler> _logger;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    public CreateMealCommandHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        IMealRepository mealRepository,
        IFoodItemRepository foodItemRepository,
        ICustomFoodRepository customFoodRepository,
        IFodmapAggregator fodmapAggregator,
        IUnitOfWork unitOfWork,
        ILogger<CreateMealCommandHandler> logger)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _mealRepository = mealRepository;
        _foodItemRepository = foodItemRepository;
        _customFoodRepository = customFoodRepository;
        _fodmapAggregator = fodmapAggregator;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<CreateMealResult> Handle(CreateMealCommand request, CancellationToken cancellationToken)
    {
        var serverUtcNow = DateTime.UtcNow;
        var patientId = await ResolveCurrentPatientIdAsync(cancellationToken).ConfigureAwait(false);

        // Pares (nivel FODMAP, peso) para la agregación. Para los ítems de catálogo se
        // usa el nivel del alimento; los alimentos personalizados se excluyen de la
        // agregación en esta versión (su nivel se derivaría de sus ingredientes; TODO).
        var fodmapInputs = new List<(FodmapLevel itemLevel, decimal weightGrams)>();
        var inputs = new List<MealItemInput>(request.Items.Count);

        foreach (var item in request.Items)
        {
            if (item.FoodId.HasValue)
            {
                var food = await _foodItemRepository.FindByIdAsync(item.FoodId.Value, cancellationToken).ConfigureAwait(false);
                if (food is null || !food.IsActive)
                {
                    throw new FoodItemNotFoundException(item.FoodId.Value);
                }

                fodmapInputs.Add((food.FodmapLevel, item.Quantity));
            }
            else if (item.CustomFoodId.HasValue)
            {
                var customFood = await _customFoodRepository.FindByIdAsync(item.CustomFoodId.Value, cancellationToken).ConfigureAwait(false)
                    ?? throw new CustomFoodNotFoundException(item.CustomFoodId.Value);

                if (customFood.PatientId != patientId)
                {
                    throw new PatientResourceAccessException();
                }
            }

            inputs.Add(new MealItemInput(item.FoodId, item.CustomFoodId, item.Quantity, item.Unit));
        }

        var meal = Meal.Register(
            Guid.NewGuid(),
            request.ClientGuid,
            patientId,
            request.MealTime,
            request.ConsumedAt,
            request.ClientCreatedAt,
            inputs,
            serverUtcNow);

        await _mealRepository.AddAsync(meal, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var aggregatedFodmap = _fodmapAggregator.AggregateForMeal(fodmapInputs);

        _logger.LogInformation("Meal {MealId} registered with {ItemCount} items.", meal.Id, meal.GetItemCount());
        return new CreateMealResult(meal.Id, aggregatedFodmap);
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
            throw new UnauthorizedAccessException("Solo un paciente puede registrar comidas.");
        }

        return user.Id;
    }
}
