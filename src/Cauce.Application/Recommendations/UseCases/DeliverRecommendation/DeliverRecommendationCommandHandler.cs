using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Common.Interfaces.Recommendations;
using Cauce.Domain.Recommendations;
using Cauce.Domain.Recommendations.Events;
using Cauce.Domain.Recommendations.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cauce.Application.Recommendations.UseCases.DeliverRecommendation;

/// <summary>
/// Handler de la entrega de una recomendación. Verifica que el paciente sea el propietario y,
/// si la recomendación venció, la expira en lugar de entregarla. Publica el evento de dominio
/// correspondiente vía outbox.
/// </summary>
public sealed class DeliverRecommendationCommandHandler : IRequestHandler<DeliverRecommendationCommand, Unit>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly IRecommendationRepository _recommendationRepository;
    private readonly IOutboxWriter _outboxWriter;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DeliverRecommendationCommandHandler> _logger;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    public DeliverRecommendationCommandHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        IRecommendationRepository recommendationRepository,
        IOutboxWriter outboxWriter,
        IUnitOfWork unitOfWork,
        ILogger<DeliverRecommendationCommandHandler> logger)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _recommendationRepository = recommendationRepository;
        _outboxWriter = outboxWriter;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Unit> Handle(DeliverRecommendationCommand request, CancellationToken cancellationToken)
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

        recommendation.Deliver(now);

        // Publicación del evento vía outbox ANTES del SaveChanges (patrón outbox, DEC-B5-04).
        await _outboxWriter.PublishAsync(
            recommendation.Id,
            nameof(Recommendation),
            new RecommendationDeliveredEvent(recommendation.Id, recommendation.PatientId, now),
            cancellationToken).ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Recommendation {RecommendationId} delivered to patient {PatientId}.", recommendation.Id, patientId);

        return Unit.Value;
    }
}
