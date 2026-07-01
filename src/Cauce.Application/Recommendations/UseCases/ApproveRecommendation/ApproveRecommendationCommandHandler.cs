using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Common.Interfaces.Patients;
using Cauce.Application.Common.Interfaces.Recommendations;
using Cauce.Domain.Recommendations.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Cauce.Application.Recommendations.UseCases.ApproveRecommendation;

/// <summary>
/// Handler de la aprobación de una recomendación. Verifica que el nutricionista esté asignado
/// al paciente y aplica la transición de aprobación.
/// </summary>
public sealed class ApproveRecommendationCommandHandler : IRequestHandler<ApproveRecommendationCommand, Unit>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly IRecommendationRepository _recommendationRepository;
    private readonly INutritionistPatientRepository _nutritionistPatientRepository;
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
        IUnitOfWork unitOfWork,
        ILogger<ApproveRecommendationCommandHandler> logger)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _recommendationRepository = recommendationRepository;
        _nutritionistPatientRepository = nutritionistPatientRepository;
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
            recommendation.Expire(now);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            throw new RecommendationExpiredException(recommendation.Id);
        }

        recommendation.Approve(nutritionistId, request.Note, now);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Recommendation {RecommendationId} approved by nutritionist {NutritionistId}.",
            recommendation.Id,
            nutritionistId);

        return Unit.Value;
    }
}
