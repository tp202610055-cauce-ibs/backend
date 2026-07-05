using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Common.Interfaces.Recommendations;
using Cauce.Domain.ClinicalRegistry.Enums;
using Cauce.Domain.Recommendations;
using Cauce.Domain.Recommendations.Events;
using Cauce.Domain.Recommendations.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cauce.Application.Recommendations.UseCases.SubmitFeedback;

/// <summary>
/// Handler del envío de retroalimentación. Verifica que el paciente sea el propietario y que la
/// recomendación esté entregada antes de incorporar la retroalimentación. Publica el evento de
/// dominio correspondiente vía outbox.
/// </summary>
public sealed class SubmitFeedbackCommandHandler : IRequestHandler<SubmitFeedbackCommand, Unit>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly IRecommendationRepository _recommendationRepository;
    private readonly IOutboxWriter _outboxWriter;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SubmitFeedbackCommandHandler> _logger;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    public SubmitFeedbackCommandHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        IRecommendationRepository recommendationRepository,
        IOutboxWriter outboxWriter,
        IUnitOfWork unitOfWork,
        ILogger<SubmitFeedbackCommandHandler> logger)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _recommendationRepository = recommendationRepository;
        _outboxWriter = outboxWriter;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Unit> Handle(SubmitFeedbackCommand request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var patientId = await RecommendationsUserContext
            .ResolvePatientIdAsync(_currentUserService, _userRepository, cancellationToken)
            .ConfigureAwait(false);

        var recommendation = await _recommendationRepository
            .GetByIdAsync(request.RecommendationId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new RecommendationNotFoundException(request.RecommendationId);

        if (recommendation.PatientId != patientId)
        {
            throw new RecommendationAccessDeniedException();
        }

        var feedback = RecommendationFeedback.Submit(
            recommendation.Id,
            request.WasApplied,
            request.Outcome,
            request.Comment,
            SyncStatus.SyncCompleted,
            now);

        // RecordFeedback valida la transición de estado y asocia la retroalimentación en memoria;
        // AddFeedbackAsync la marca explícitamente como inserción para Entity Framework, ya que la
        // recomendación se cargó sin la navegación de retroalimentación (evita que EF la trate como
        // actualización de una fila inexistente).
        recommendation.RecordFeedback(feedback, now);
        await _recommendationRepository.AddFeedbackAsync(feedback, cancellationToken).ConfigureAwait(false);

        // Publicación del evento vía outbox ANTES del SaveChanges (patrón outbox, DEC-B5-04).
        await _outboxWriter.PublishAsync(
            recommendation.Id,
            nameof(Recommendation),
            new RecommendationFeedbackReceivedEvent(recommendation.Id, recommendation.PatientId, request.Outcome, request.WasApplied, now),
            cancellationToken).ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Feedback recorded for recommendation {RecommendationId} by patient {PatientId}.",
            recommendation.Id,
            patientId);

        return Unit.Value;
    }
}
