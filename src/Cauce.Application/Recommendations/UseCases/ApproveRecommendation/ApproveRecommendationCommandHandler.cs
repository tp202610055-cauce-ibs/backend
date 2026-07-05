using Cauce.Application.Common.Auditing;
using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Common.Interfaces.Patients;
using Cauce.Application.Common.Interfaces.Recommendations;
using Cauce.Domain.Auditing.Enums;
using Cauce.Domain.Recommendations;
using Cauce.Domain.Recommendations.Events;
using Cauce.Domain.Recommendations.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cauce.Application.Recommendations.UseCases.ApproveRecommendation;

/// <summary>
/// Handler de la aprobación de una recomendación. Verifica que el nutricionista esté asignado
/// al paciente, aplica la transición de aprobación, audita el flujo HITL y publica el evento de
/// dominio vía outbox para notificar al paciente de forma asíncrona.
/// </summary>
public sealed class ApproveRecommendationCommandHandler : IRequestHandler<ApproveRecommendationCommand, Unit>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly IRecommendationRepository _recommendationRepository;
    private readonly INutritionistPatientRepository _nutritionistPatientRepository;
    private readonly IAuditLogger _auditLogger;
    private readonly IOutboxWriter _outboxWriter;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ApproveRecommendationCommandHandler> _logger;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    public ApproveRecommendationCommandHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        IRecommendationRepository recommendationRepository,
        INutritionistPatientRepository nutritionistPatientRepository,
        IAuditLogger auditLogger,
        IOutboxWriter outboxWriter,
        IUnitOfWork unitOfWork,
        ILogger<ApproveRecommendationCommandHandler> logger)
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
    public async Task<Unit> Handle(ApproveRecommendationCommand request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var nutritionistId = await RecommendationsUserContext
            .ResolveNutritionistIdAsync(_currentUserService, _userRepository, cancellationToken)
            .ConfigureAwait(false);

        var recommendation = await _recommendationRepository
            .GetByIdAsync(request.RecommendationId, cancellationToken)
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

        recommendation.Approve(nutritionistId, request.Note, now);

        // Auditoría del flujo HITL (capa 3) y publicación del evento vía outbox, ambas ANTES del
        // SaveChanges para persistir de forma atómica el cambio, la bitácora y el mensaje de outbox.
        // La nota clínica se registra como hash (no en claro) por la Ley N.° 29733 (acta A8).
        await _auditLogger.LogAsync(
            AuditActionType.Approve,
            nameof(Recommendation),
            recommendation.Id,
            oldValuesHash: null,
            newValuesHash: AuditHash.Sha256Hex(request.Note),
            additionalContext: null,
            cancellationToken).ConfigureAwait(false);

        await _outboxWriter.PublishAsync(
            recommendation.Id,
            nameof(Recommendation),
            new RecommendationApprovedEvent(recommendation.Id, recommendation.PatientId, nutritionistId, recommendation.AutoApproved, now),
            cancellationToken).ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Recommendation {RecommendationId} approved by nutritionist {NutritionistId}.",
            recommendation.Id,
            nutritionistId);

        return Unit.Value;
    }
}
