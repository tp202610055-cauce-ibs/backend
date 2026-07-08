using System.Text.Json;
using Cauce.Application.Common.Auditing;
using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Common.Interfaces.Patients;
using Cauce.Application.Common.Interfaces.Recommendations;
using Cauce.Application.Recommendations.UseCases;
using Cauce.Domain.Auditing.Enums;
using Cauce.Domain.Recommendations;
using Cauce.Domain.Recommendations.Events;
using Cauce.Domain.Recommendations.Exceptions;
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
        IAuditLogger auditLogger,
        IOutboxWriter outboxWriter,
        IUnitOfWork unitOfWork,
        ILogger<ModifyRecommendationCommandHandler> logger)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _recommendationRepository = recommendationRepository;
        _nutritionistPatientRepository = nutritionistPatientRepository;
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

        recommendation.ModifyByNutritionist(
            nutritionistId, request.ClinicalNote, now, newItems, request.Title, request.Description, request.Steps);

        await _auditLogger.LogAsync(
            AuditActionType.Approve,
            nameof(Recommendation),
            recommendation.Id,
            oldValuesHash: null,
            newValuesHash: AuditHash.Sha256Hex(request.ClinicalNote),
            additionalContext: JsonSerializer.Serialize(new { operation = "modified_approved" }),
            cancellationToken).ConfigureAwait(false);

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
}
