using System.Text.Json;
using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.ClinicalRegistry;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Common.Interfaces.Patients;
using Cauce.Domain.Auditing.Enums;
using Cauce.Domain.ClinicalRegistry;
using Cauce.Domain.ClinicalRegistry.Exceptions;
using Cauce.Domain.Identity;
using Cauce.Domain.Patients.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cauce.Application.ClinicalRegistry.UseCases.SetSymptomMealAssociation;

/// <summary>
/// Handler de la corrección manual de la comida asociada a un síntoma. Verifica que el nutricionista esté
/// asignado al paciente del síntoma y que la comida, si se indica, sea de ese mismo paciente; no aplica la
/// ventana de 4 horas, porque es una decisión clínica explícita y no una correlación automática. Audita la
/// corrección de forma explícita antes del <c>SaveChanges</c>, igual que el flujo HITL de recomendaciones.
/// </summary>
public sealed class SetSymptomMealAssociationCommandHandler : IRequestHandler<SetSymptomMealAssociationCommand, Unit>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly ISymptomRepository _symptomRepository;
    private readonly IMealRepository _mealRepository;
    private readonly INutritionistPatientRepository _nutritionistPatientRepository;
    private readonly IAuditLogger _auditLogger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SetSymptomMealAssociationCommandHandler> _logger;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    public SetSymptomMealAssociationCommandHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        ISymptomRepository symptomRepository,
        IMealRepository mealRepository,
        INutritionistPatientRepository nutritionistPatientRepository,
        IAuditLogger auditLogger,
        IUnitOfWork unitOfWork,
        ILogger<SetSymptomMealAssociationCommandHandler> logger)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _symptomRepository = symptomRepository;
        _mealRepository = mealRepository;
        _nutritionistPatientRepository = nutritionistPatientRepository;
        _auditLogger = auditLogger;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Unit> Handle(SetSymptomMealAssociationCommand request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var nutritionistId = await ResolveCurrentNutritionistIdAsync(cancellationToken).ConfigureAwait(false);

        var symptom = await _symptomRepository
            .FindByIdForUpdateAsync(request.SymptomId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new SymptomNotFoundException(request.SymptomId);

        var isAssigned = await _nutritionistPatientRepository
            .ActiveAssignmentExistsAsync(nutritionistId, symptom.PatientId, cancellationToken)
            .ConfigureAwait(false);
        if (!isAssigned)
        {
            throw new PatientAccessNotAuthorizedException();
        }

        var previousMealId = symptom.AssociatedMealId;

        if (request.MealId is { } mealId)
        {
            await EnsureMealBelongsToPatientAsync(mealId, symptom.PatientId, cancellationToken).ConfigureAwait(false);
            symptom.AssociateWithMeal(mealId, now);
        }
        else
        {
            symptom.ClearMealAssociation(now);
        }

        // Auditoría explícita ANTES del SaveChanges, para que la corrección y su bitácora se persistan de
        // forma atómica (acta A8). El trigger de symptoms deja además su propia fila con hashes; esta guarda
        // la comida anterior y la nueva, que el trigger no conserva (ver AuditActionType.MealAssociationCorrection).
        await _auditLogger.LogAsync(
            AuditActionType.MealAssociationCorrection,
            nameof(Symptom),
            symptom.Id,
            oldValuesHash: null,
            newValuesHash: null,
            additionalContext: JsonSerializer.Serialize(new
            {
                previous_meal_id = previousMealId,
                new_meal_id = request.MealId
            }),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Symptom {SymptomId} meal association set by {NutritionistId} (associated: {HasAssociation}).",
            symptom.Id,
            nutritionistId,
            symptom.HasMealAssociation);

        return Unit.Value;
    }

    private async Task EnsureMealBelongsToPatientAsync(Guid mealId, Guid patientId, CancellationToken ct)
    {
        var meal = await _mealRepository.FindByIdAsync(mealId, ct).ConfigureAwait(false)
            ?? throw new MealNotFoundException(mealId);

        if (meal.PatientId != patientId)
        {
            throw new PatientResourceAccessException("La comida indicada no pertenece al paciente del síntoma.");
        }
    }

    private async Task<Guid> ResolveCurrentNutritionistIdAsync(CancellationToken ct)
    {
        var keycloakId = _currentUserService.UserId
            ?? throw new UnauthorizedAccessException("No hay un usuario autenticado en la petición.");

        var user = await _userRepository.FindByKeycloakIdAsync(keycloakId.ToString(), ct).ConfigureAwait(false)
            ?? throw new UnauthorizedAccessException("El usuario autenticado no tiene una cuenta local asociada.");

        var nutritionistRoleId = await _userRepository.GetRoleIdAsync(UserRoles.Nutritionist, ct).ConfigureAwait(false);
        if (user.RoleId != nutritionistRoleId)
        {
            throw new UnauthorizedAccessException("Solo un nutricionista puede corregir la asociación de un síntoma.");
        }

        return user.Id;
    }
}
