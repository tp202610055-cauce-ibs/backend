using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Common.Interfaces.Patients;
using Cauce.Application.Common.Interfaces.Recommendations;
using Cauce.Domain.Recommendations.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cauce.Application.Recommendations.UseCases.RejectRecommendation;

/// <summary>
/// Handler del rechazo de una recomendación. Verifica que el nutricionista esté asignado al
/// paciente y aplica la transición de rechazo.
/// </summary>
public sealed class RejectRecommendationCommandHandler : IRequestHandler<RejectRecommendationCommand, Unit>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly IRecommendationRepository _recommendationRepository;
    private readonly INutritionistPatientRepository _nutritionistPatientRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RejectRecommendationCommandHandler> _logger;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    public RejectRecommendationCommandHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        IRecommendationRepository recommendationRepository,
        INutritionistPatientRepository nutritionistPatientRepository,
        IUnitOfWork unitOfWork,
        ILogger<RejectRecommendationCommandHandler> logger)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _recommendationRepository = recommendationRepository;
        _nutritionistPatientRepository = nutritionistPatientRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Unit> Handle(RejectRecommendationCommand request, CancellationToken cancellationToken)
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
            recommendation.Expire(now);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            throw new RecommendationExpiredException(recommendation.Id);
        }

        recommendation.Reject(nutritionistId, request.Reason, now);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Recommendation {RecommendationId} rejected by nutritionist {NutritionistId}.",
            recommendation.Id,
            nutritionistId);

        return Unit.Value;
    }
}
