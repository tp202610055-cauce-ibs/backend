using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Common.Interfaces.Recommendations;
using Cauce.Application.Recommendations.Dtos;
using MediatR;

namespace Cauce.Application.Recommendations.UseCases.GetPatientReviewStatus;

/// <summary>
/// Handler de <see cref="GetPatientReviewStatusQuery"/>. Solo lee: no transita las vencidas.
/// </summary>
public sealed class GetPatientReviewStatusQueryHandler
    : IRequestHandler<GetPatientReviewStatusQuery, RecommendationReviewStatusDto>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly IRecommendationRepository _recommendationRepository;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    public GetPatientReviewStatusQueryHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        IRecommendationRepository recommendationRepository)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _recommendationRepository = recommendationRepository;
    }

    /// <inheritdoc />
    public async Task<RecommendationReviewStatusDto> Handle(
        GetPatientReviewStatusQuery request,
        CancellationToken cancellationToken)
    {
        var patientId = await RecommendationsUserContext
            .ResolvePatientIdAsync(_currentUserService, _userRepository, cancellationToken)
            .ConfigureAwait(false);

        var inReview = await _recommendationRepository
            .ExistsInReviewForPatientAsync(patientId, DateTime.UtcNow, cancellationToken)
            .ConfigureAwait(false);

        return new RecommendationReviewStatusDto(inReview);
    }
}
