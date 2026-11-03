using System.Text.Json;
using Cauce.Application.Common.Auditing;
using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.ClinicalRegistry;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Common.Interfaces.Patients;
using Cauce.Application.Common.Interfaces.Recommendations;
using Cauce.Application.Recommendations.UseCases;
using Cauce.Domain.Auditing.Enums;
using Cauce.Domain.Recommendations;
using Cauce.Domain.Recommendations.Events;
using Cauce.Domain.Recommendations.Exceptions;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cauce.Application.Recommendations.UseCases.ModifyRecommendation;

/// <summary>
/// Handler de la aprobación con modificación de una recomendación (US17 CA03). Verifica la asignación
/// nutricionista-paciente, aplica la transición a <c>ModifiedApproved</c> reemplazando ítems y/o
/// contenido, audita el flujo HITL y publica el evento de dominio vía outbox para notificar al paciente.
/// </summary>
public sealed class ModifyRecommendationCommandHandler : IRequestHandler<ModifyRecommendationCommand, Unit>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly IRecommendationRepository _recommendationRepository;
    private readonly INutritionistPatientRepository _nutritionistPatientRepository;
    private readonly IFoodItemRepository _foodItemRepository;
    private readonly IAuditLogger _auditLogger;
    private readonly IOutboxWriter _outboxWriter;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ModifyRecommendationCommandHandler> _logger;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    public ModifyRecommendationCommandHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        IRecommendationRepository recommendationRepository,
        INutritionistPatientRepository nutritionistPatientRepository,
        IFoodItemRepository foodItemRepository,
        IAuditLogger auditLogger,
        IOutboxWriter outboxWriter,
        IUnitOfWork unitOfWork,
        ILogger<ModifyRecommendationCommandHandler> logger)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _recommendationRepository = recommendationRepository;
        _nutritionistPatientRepository = nutritionistPatientRepository;
        _foodItemRepository = foodItemRepository;
        _auditLogger = auditLogger;
        _outboxWriter = outboxWriter;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Unit> Handle(ModifyRecommendationCommand request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var nutritionistId = await RecommendationsUserContext
            .ResolveNutritionistIdAsync(_currentUserService, _userRepository, cancellationToken)
            .ConfigureAwait(false);

        var recommendation = await _recommendationRepository
            .GetByIdWithDetailsAsync(request.RecommendationId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new RecommendationNotFoundException(request.RecommendationId);

        var isAssigned = await _nutritionistPatientRepository
            .ActiveAssignmentExistsAsync(nutritionistId, recommendation.PatientId, cancellationToken)
            .ConfigureAwait(false);
        if (!isAssigned)
        {
            throw new RecommendationAccessDeniedException();
        }

        if (recommendation.IsExpired(now))
        {
            var previousStatus = recommendation.Status;
            recommendation.Expire(now);
            await _outboxWriter.PublishAsync(
                recommendation.Id,
                nameof(Recommendation),
                new RecommendationExpiredEvent(recommendation.Id, recommendation.PatientId, previousStatus, now),
                cancellationToken).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            throw new RecommendationExpiredException(recommendation.Id);
        }

        var newItems = request.Items?
            .Select(item => RecommendationItem.Create(item.FoodId, item.ActionType, item.Reasoning, item.SubstituteFoodId))
            .ToList();
        await EnsureFoodsExistAsync(request.Items, cancellationToken).ConfigureAwait(false);

        var clinicalNote = request.ClinicalNote.Trim();
        recommendation.ModifyByNutritionist(
            nutritionistId, clinicalNote, now, newItems, request.Title, request.Description, request.Steps);
        _recommendationRepository.Update(recommendation);

        await _auditLogger.LogAsync(
            AuditActionType.Approve,
            nameof(Recommendation),
            recommendation.Id,
            oldValuesHash: null,
            newValuesHash: AuditHash.Sha256Hex(clinicalNote),
            additionalContext: JsonSerializer.Serialize(new { operation = "modified_approved" }),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        await _outboxWriter.PublishAsync(
            recommendation.Id,
            nameof(Recommendation),
            new RecommendationModifiedApprovedEvent(recommendation.Id, recommendation.PatientId, nutritionistId, now),
            cancellationToken).ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Recommendation {RecommendationId} modified and approved by nutritionist {NutritionistId}.",
            recommendation.Id,
            nutritionistId);

        return Unit.Value;
    }

    // Un alimento fuera del catálogo violaría la clave foránea al guardar; se informa como error de validación.
    private async Task EnsureFoodsExistAsync(IReadOnlyList<ModifyRecommendationItemInput>? items, CancellationToken ct)
    {
        if (items is null)
        {
            return;
        }

        var foodIds = items
            .Select(item => item.FoodId)
            .Concat(items.Where(item => item.SubstituteFoodId.HasValue).Select(item => item.SubstituteFoodId!.Value))
            .Distinct();
        var missing = new HashSet<Guid>();
        foreach (var foodId in foodIds)
        {
            if (await _foodItemRepository.FindByIdAsync(foodId, ct).ConfigureAwait(false) is null)
            {
                missing.Add(foodId);
            }
        }

        var failures = new List<ValidationFailure>();
        for (var index = 0; index < items.Count; index++)
        {
            if (missing.Contains(items[index].FoodId))
            {
                failures.Add(new ValidationFailure($"Items[{index}].FoodId", "El alimento no existe en el catálogo."));
            }

            if (items[index].SubstituteFoodId is { } substituteFoodId && missing.Contains(substituteFoodId))
            {
                failures.Add(new ValidationFailure(
                    $"Items[{index}].SubstituteFoodId", "El alimento sustituto no existe en el catálogo."));
            }
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }
    }
}
