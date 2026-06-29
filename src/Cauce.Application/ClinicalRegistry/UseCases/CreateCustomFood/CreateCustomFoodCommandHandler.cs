using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.ClinicalRegistry;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Domain.Auditing.Enums;
using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry.Exceptions;
using Cauce.Domain.Identity;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cauce.Application.ClinicalRegistry.UseCases.CreateCustomFood;

/// <summary>
/// Handler de la creación de un alimento personalizado. Valida la unicidad del nombre por
/// paciente y la existencia de cada alimento del catálogo referenciado; registra el
/// evento en auditoría.
/// </summary>
public sealed class CreateCustomFoodCommandHandler : IRequestHandler<CreateCustomFoodCommand, CreateCustomFoodResult>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly ICustomFoodRepository _customFoodRepository;
    private readonly IFoodItemRepository _foodItemRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogger _auditLogger;
    private readonly ILogger<CreateCustomFoodCommandHandler> _logger;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    public CreateCustomFoodCommandHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        ICustomFoodRepository customFoodRepository,
        IFoodItemRepository foodItemRepository,
        IUnitOfWork unitOfWork,
        IAuditLogger auditLogger,
        ILogger<CreateCustomFoodCommandHandler> logger)
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
    public async Task<CreateCustomFoodResult> Handle(CreateCustomFoodCommand request, CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;
        var patientId = await ResolveCurrentPatientIdAsync(cancellationToken).ConfigureAwait(false);

        if (await _customFoodRepository.ExistsByPatientAndNameAsync(patientId, request.Name, cancellationToken).ConfigureAwait(false))
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

        var customFood = CustomFood.Create(Guid.NewGuid(), patientId, request.Name, request.PortionSizeGrams, utcNow);
        foreach (var ingredient in request.Ingredients)
        {
            customFood.AddIngredient(ingredient.FoodId, ingredient.ProportionGrams);
        }

        await _customFoodRepository.AddAsync(customFood, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await _auditLogger.LogAsync(
            AuditActionType.Create, nameof(CustomFood), customFood.Id,
            oldValuesHash: null, newValuesHash: null, additionalContext: null, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Custom food {CustomFoodId} created with {IngredientCount} ingredients.",
            customFood.Id, request.Ingredients.Count);

        return new CreateCustomFoodResult(customFood.Id);
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
            throw new UnauthorizedAccessException("Solo un paciente puede crear alimentos personalizados.");
        }

        return user.Id;
    }
}
