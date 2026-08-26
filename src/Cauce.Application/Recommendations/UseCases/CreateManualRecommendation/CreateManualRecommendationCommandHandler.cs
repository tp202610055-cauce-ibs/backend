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

namespace Cauce.Application.Recommendations.UseCases.CreateManualRecommendation;

/// <summary>
/// Handler de la creación manual de una recomendación (US29). Verifica que el nutricionista esté
/// asignado al paciente, crea la recomendación ya aprobada, audita la aprobación con origen manual y
/// publica el evento de dominio vía outbox para notificar al paciente.
/// </summary>
public sealed class CreateManualRecommendationCommandHandler
    : IRequestHandler<CreateManualRecommendationCommand, CreateManualRecommendationResult>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly IRecommendationRepository _recommendationRepository;
    private readonly INutritionistPatientRepository _nutritionistPatientRepository;
    private readonly IAuditLogger _auditLogger;
    private readonly IOutboxWriter _outboxWriter;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateManualRecommendationCommandHandler> _logger;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    public CreateManualRecommendationCommandHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        IRecommendationRepository recommendationRepository,
        INutritionistPatientRepository nutritionistPatientRepository,
        IAuditLogger auditLogger,
        IOutboxWriter outboxWriter,
        IUnitOfWork unitOfWork,
        ILogger<CreateManualRecommendationCommandHandler> logger)
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
    public async Task<CreateManualRecommendationResult> Handle(
        CreateManualRecommendationCommand request,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var nutritionistId = await RecommendationsUserContext
            .ResolveNutritionistIdAsync(_currentUserService, _userRepository, cancellationToken)
            .ConfigureAwait(false);

        var isAssigned = await _nutritionistPatientRepository
            .ActiveAssignmentExistsAsync(nutritionistId, request.PatientId, cancellationToken)
            .ConfigureAwait(false);
        if (!isAssigned)
        {
            throw new RecommendationAccessDeniedException();
        }

        var recommendation = Recommendation.CreateManual(
            request.PatientId,
            nutritionistId,
            request.Title,
            request.Description,
            request.Steps,
            request.ClinicalNote,
            request.ValidUntil,
            now);

        await _recommendationRepository.AddAsync(recommendation, cancellationToken).ConfigureAwait(false);

        // Auditoría del flujo HITL (capa 3) y publicación del evento vía outbox, ambas ANTES del
        // SaveChanges para persistir de forma atómica. La nota clínica se registra como hash (acta A8).
        await _auditLogger.LogAsync(
            AuditActionType.Approve,
            nameof(Recommendation),
            recommendation.Id,
            oldValuesHash: null,
            newValuesHash: AuditHash.Sha256Hex(request.ClinicalNote),
            additionalContext: JsonSerializer.Serialize(new { source = "manual" }),
            cancellationToken: cancellationToken).ConfigureAwait(false);

        await _outboxWriter.PublishAsync(
            recommendation.Id,
            nameof(Recommendation),
            new RecommendationManuallyCreatedEvent(recommendation.Id, request.PatientId, nutritionistId, now),
            cancellationToken).ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Manual recommendation {RecommendationId} created by nutritionist {NutritionistId}.",
            recommendation.Id,
            nutritionistId);

        return new CreateManualRecommendationResult(recommendation.Id, recommendation.Status);
    }
}
