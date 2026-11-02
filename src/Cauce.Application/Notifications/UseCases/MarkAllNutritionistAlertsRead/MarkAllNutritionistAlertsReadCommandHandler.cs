using Cauce.Application.Common.Interfaces;
using Cauce.Application.Common.Interfaces.Identity;
using Cauce.Application.Common.Interfaces.Notifications;
using MediatR;

namespace Cauce.Application.Notifications.UseCases.MarkAllNutritionistAlertsRead;

/// <summary>
/// Handler de <see cref="MarkAllNutritionistAlertsReadCommand"/>.
/// </summary>
public sealed class MarkAllNutritionistAlertsReadCommandHandler : IRequestHandler<MarkAllNutritionistAlertsReadCommand, Unit>
{
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserRepository _userRepository;
    private readonly INutritionistAlertRepository _alertRepository;
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>
    /// Inicializa el handler con sus dependencias.
    /// </summary>
    public MarkAllNutritionistAlertsReadCommandHandler(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        INutritionistAlertRepository alertRepository,
        IUnitOfWork unitOfWork)
    {
        _currentUserService = currentUserService;
        _userRepository = userRepository;
        _alertRepository = alertRepository;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task<Unit> Handle(MarkAllNutritionistAlertsReadCommand request, CancellationToken cancellationToken)
    {
        var nutritionistId = await NotificationsUserContext
            .ResolveNutritionistIdAsync(_currentUserService, _userRepository, cancellationToken)
            .ConfigureAwait(false);

        var now = DateTime.UtcNow;
        var unread = await _alertRepository.ListUnreadAsync(nutritionistId, cancellationToken).ConfigureAwait(false);
        foreach (var alert in unread)
        {
            alert.MarkAsRead(now);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }
}
